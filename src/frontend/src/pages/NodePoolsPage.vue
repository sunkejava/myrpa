<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Pool = { id: string; name: string; description: string | null; enabled: boolean; nodeCount: number }
type Node = { id: string; name: string; status: string; nodePoolId: string | null }
const props = defineProps<{ token: string }>()
const pools = ref<Pool[]>([])
const nodes = ref<Node[]>([])
const name = ref('')
const description = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function call<T>(path: string, method = 'GET', data?: object): Promise<T> {
  const response = await fetch(`/api/node-management/${path}`, { method,
    headers: { Authorization: `Bearer ${props.token}`, ...(data ? { 'Content-Type': 'application/json' } : {}) },
    ...(data ? { body: JSON.stringify(data) } : {}) })
  const content = await response.text()
  let result: T | { message?: string } | null = null
  try { result = content ? JSON.parse(content) : null } catch { /* 删除成功时没有正文 */ }
  if (!response.ok) throw new Error(result && typeof result === 'object' && 'message' in result ? String(result.message) : content || `请求失败 (${response.status})`)
  return result as T
}
async function load() {
  try { [pools.value, nodes.value] = await Promise.all([call<Pool[]>('pools'), call<Node[]>('nodes')]) }
  catch (e) { error.value = e instanceof Error ? e.message : '节点池数据加载失败' }
}
async function run(work: () => Promise<void>, message: string) {
  busy.value = true; error.value = ''; notice.value = ''
  try { await work(); await load(); notice.value = message }
  catch (e) { error.value = e instanceof Error ? e.message : '操作失败' }
  finally { busy.value = false }
}
async function create() {
  await run(async () => {
    await call('pools', 'POST', { name: name.value, description: description.value })
    name.value = ''; description.value = ''
  }, '节点池已创建。')
}
async function update(pool: Pool, enabled: boolean) {
  await run(async () => { await call(`pools/${pool.id}`, 'PUT', { name: pool.name, description: pool.description, enabled }) },
    enabled ? '节点池已启用。' : '节点池已停用；其中节点不再参与新任务调度。')
}
async function rename(pool: Pool) {
  const next = window.prompt('节点池名称', pool.name)
  if (!next || next.trim() === pool.name) return
  await run(async () => { await call(`pools/${pool.id}`, 'PUT', { name: next.trim(), description: pool.description, enabled: pool.enabled }) }, '节点池已重命名。')
}
async function remove(pool: Pool) {
  if (!window.confirm(`删除空节点池「${pool.name}」？`)) return
  await run(async () => { await call(`pools/${pool.id}`, 'DELETE') }, '节点池已删除。')
}
async function assign(node: Node, poolId: string) {
  await run(async () => { await call(`nodes/${node.id}/pool`, 'POST', { poolId: poolId || null }) }, `节点「${node.name}」的归属已更新。`)
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel">
    <div class="panel-title"><span>节点池</span><button class="action-btn" @click="load">刷新</button></div>
    <p class="muted">停用节点池会阻止其中节点接收新任务；进行中的执行继续完成。节点归属由管理员设置，Agent 重连不会覆盖。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <form @submit.prevent="create"><div class="resource-grid"><label>节点池名称<input v-model.trim="name" required maxlength="128" /></label><label>说明<input v-model.trim="description" /></label></div><button class="action-btn primary" :disabled="busy">创建节点池</button></form>
    <div class="table-wrap"><table><thead><tr><th>节点池</th><th>节点数</th><th>状态</th><th>操作</th></tr></thead><tbody><tr v-for="pool in pools" :key="pool.id"><td>{{ pool.name }}<small>{{ pool.description || pool.id }}</small></td><td>{{ pool.nodeCount }}</td><td>{{ pool.enabled ? '启用' : '停用' }}</td><td><button class="action-btn" :disabled="busy" @click="rename(pool)">重命名</button><button class="action-btn" :disabled="busy" @click="update(pool, !pool.enabled)">{{ pool.enabled ? '停用' : '启用' }}</button><button class="action-btn" :disabled="busy || pool.nodeCount > 0" @click="remove(pool)">删除</button></td></tr></tbody></table><p v-if="!pools.length" class="muted empty">暂无节点池。</p></div>
    <h3>节点归属</h3>
    <div class="table-wrap"><table><thead><tr><th>节点</th><th>状态</th><th>所属节点池</th></tr></thead><tbody><tr v-for="node in nodes" :key="node.id"><td>{{ node.name }}<small>{{ node.id }}</small></td><td>{{ node.status }}</td><td><select :aria-label="`节点池归属 ${node.id}`" :value="node.nodePoolId || ''" :disabled="busy" @change="assign(node, ($event.target as HTMLSelectElement).value)"><option value="">不指定节点池</option><option v-for="pool in pools" :key="pool.id" :value="pool.id" :disabled="!pool.enabled && pool.id !== node.nodePoolId">{{ pool.name }}{{ pool.enabled ? '' : '（停用）' }}</option></select></td></tr></tbody></table></div>
  </section>
</template>
