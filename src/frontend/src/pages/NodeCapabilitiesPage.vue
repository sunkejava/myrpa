<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Node = { id: string; name: string; status: string }
type Capability = { id: string; code: string; version: string | null; enabled: boolean; metadataJson: string | null }
const props = defineProps<{ token: string }>()
const nodes = ref<Node[]>([])
const selected = ref('')
const capabilities = ref<Capability[]>([])
const busy = ref(false)
const error = ref('')
const notice = ref('')

async function call<T>(path: string, method = 'GET', body?: object): Promise<T> {
  const response = await fetch(`/api/node-management/${path}`, { method,
    headers: { Authorization: `Bearer ${props.token}`, ...(body ? { 'Content-Type': 'application/json' } : {}) },
    ...(body ? { body: JSON.stringify(body) } : {}) })
  const content = await response.text()
  let result: T | { message?: string } | null = null
  try { result = content ? JSON.parse(content) : null } catch { /* 允许空响应 */ }
  if (!response.ok) throw new Error(result && typeof result === 'object' && 'message' in result ? String(result.message) : `请求失败 (${response.status})`)
  return result as T
}
async function loadNodes() {
  try { nodes.value = await call<Node[]>('nodes') }
  catch (e) { error.value = e instanceof Error ? e.message : '节点加载失败' }
}
async function selectNode(id: string) {
  selected.value = id; capabilities.value = []; error.value = ''
  if (!id) return
  try { capabilities.value = (await call<{ capabilities: Capability[] }>(`nodes/${id}`)).capabilities }
  catch (e) { error.value = e instanceof Error ? e.message : '能力加载失败' }
}
async function toggle(capability: Capability) {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`nodes/${selected.value}/capabilities/${capability.id}/enabled`, 'POST', { enabled: !capability.enabled })
    await selectNode(selected.value)
    notice.value = `能力「${capability.code}」已${capability.enabled ? '禁用' : '启用'}。`
  } catch (e) { error.value = e instanceof Error ? e.message : '能力设置失败' }
  finally { busy.value = false }
}
onMounted(loadNodes)
</script>

<template>
  <section class="panel form-panel">
    <div class="panel-title"><span>节点能力</span><button class="action-btn" @click="selected ? selectNode(selected) : loadNodes()">刷新</button></div>
    <p class="muted">能力由 NodeAgent 上报；管理员可禁用不可信能力。禁用设置在 Agent 重连后仍有效，只有管理员明确启用才能再次参与调度。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <label>节点<select aria-label="选择执行节点" :value="selected" @change="selectNode(($event.target as HTMLSelectElement).value)"><option value="">选择节点</option><option v-for="node in nodes" :key="node.id" :value="node.id">{{ node.name }} · {{ node.id }}</option></select></label>
    <div v-if="selected" class="table-wrap"><table><thead><tr><th>能力代码</th><th>版本</th><th>状态</th><th>操作</th></tr></thead><tbody><tr v-for="item in capabilities" :key="item.id"><td>{{ item.code }}</td><td>{{ item.version || '—' }}</td><td>{{ item.enabled ? '启用' : '禁用' }}</td><td><button class="action-btn" :disabled="busy" @click="toggle(item)">{{ item.enabled ? '禁用' : '启用' }}</button></td></tr></tbody></table><p v-if="!capabilities.length" class="muted empty">节点尚未上报能力。</p></div>
  </section>
</template>
