<script setup lang="ts">
import { onMounted, ref } from 'vue'
import FormDialog from '../components/common/FormDialog.vue'
import ConfirmAction from '../components/common/ConfirmAction.vue'
import DataTable from '../components/table/DataTable.vue'

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
const renameTarget = ref<Pool | null>(null)
const nextName = ref('')
const columns = [{ key: 'name', label: '节点池', sortable: true, filterable: true },
  { key: 'description', label: '说明' }, { key: 'nodeCount', label: '节点数', sortable: true },
  { key: 'enabled', label: '状态', format: (value: unknown) => value ? '启用' : '停用' }]

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
async function rename() {
  const pool = renameTarget.value
  if (!pool || !nextName.value.trim() || nextName.value.trim() === pool.name) { renameTarget.value = null; return }
  await run(async () => { await call(`pools/${pool.id}`, 'PUT', { name: nextName.value.trim(), description: pool.description, enabled: pool.enabled }) }, '节点池已重命名。')
  if (!error.value) renameTarget.value = null
}
function edit(pool: Pool) { renameTarget.value = pool; nextName.value = pool.name }
async function remove(pool: Pool) {
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
    <DataTable :rows="pools" :columns="columns" :loading="busy" empty-label="暂无节点池。" filename="node-pools.csv" @refresh="load"><template #actions="{ row }"><button class="action-btn" :disabled="busy" @click="edit(row as Pool)">重命名</button><button class="action-btn" :disabled="busy" @click="update(row as Pool, !(row as Pool).enabled)">{{ (row as Pool).enabled ? '停用' : '启用' }}</button><ConfirmAction label="删除" title="删除节点池" :message="`删除空节点池「${(row as Pool).name}」？`" :disabled="busy || (row as Pool).nodeCount > 0" @confirmed="remove(row as Pool)" /></template></DataTable>
    <FormDialog :open="!!renameTarget" title="重命名节点池" :busy="busy" @close="renameTarget = null" @submit="rename"><label>节点池名称<input v-model.trim="nextName" required maxlength="128" /></label></FormDialog>
    <h3>节点归属</h3>
    <div class="table-wrap"><table><thead><tr><th>节点</th><th>状态</th><th>所属节点池</th></tr></thead><tbody><tr v-for="node in nodes" :key="node.id"><td>{{ node.name }}<small>{{ node.id }}</small></td><td>{{ node.status }}</td><td><select :aria-label="`节点池归属 ${node.id}`" :value="node.nodePoolId || ''" :disabled="busy" @change="assign(node, ($event.target as HTMLSelectElement).value)"><option value="">不指定节点池</option><option v-for="pool in pools" :key="pool.id" :value="pool.id" :disabled="!pool.enabled && pool.id !== node.nodePoolId">{{ pool.name }}{{ pool.enabled ? '' : '（停用）' }}</option></select></td></tr></tbody></table></div>
  </section>
</template>
