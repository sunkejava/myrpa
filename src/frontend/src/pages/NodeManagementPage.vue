<script setup lang="ts">
import { onMounted, ref } from 'vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import NodeDetailPage from './NodeDetailPage.vue'
import RobotStatusCard from '../components/robot/RobotStatusCard.vue'
import DetailDrawer from '../components/common/DetailDrawer.vue'

type Node = { id: string; name: string; nodeKind: string; osPlatform: string; status: string; networkZone: string | null;
  lastHeartbeatAt: string | null; cpuUsage: number | null; memoryUsage: number | null; reportedAvailableSlots: number | null; capabilities: Array<{ code: string }> }
type Slot = { id: string; nodeId: string; slotName: string; enabled: boolean; executionId: string | null }
const props = defineProps<{ token: string }>()
const nodes = ref<Node[]>([])
const slots = ref<Slot[]>([])
const busy = ref(false)
const error = ref('')
const notice = ref('')
const selectedNodeId = ref('')

async function call<T>(path: string, method = 'GET', data?: object): Promise<T> {
  const response = await fetch(`/api/${path}`, { method, headers: { Authorization: `Bearer ${props.token}`,
    ...(data ? { 'Content-Type': 'application/json' } : {}) }, ...(data ? { body: JSON.stringify(data) } : {}) })
  if (!response.ok) {
    const details: unknown = await response.json().catch(() => null)
    throw new Error(details && typeof details === 'object' && 'message' in details ? String(details.message) : `请求失败 (${response.status})`)
  }
  const content = await response.text()
  return (content ? JSON.parse(content) : undefined) as T
}
async function load() {
  error.value = ''
  try { [nodes.value, slots.value] = await Promise.all([call<Node[]>('node-management/nodes'), call<Slot[]>('node-management/slots')]) }
  catch (e) { error.value = e instanceof Error ? e.message : '节点数据加载失败' }
}
async function change(node: Node, action: 'approve' | 'reject' | 'revoke' | 'drain' | 'restore') {
  if (action === 'revoke' && !window.confirm(`确认吊销节点「${node.name}」？旧 AgentKey 将无法再次注册。`)) return
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`nodes/${node.id}/${action === 'restore' ? 'status' : action}`, 'POST', action === 'restore' ? { status: 'Online' } : undefined)
    await load()
    notice.value = `节点「${node.name}」操作成功。`
  } catch (e) { error.value = e instanceof Error ? e.message : '节点操作失败' }
  finally { busy.value = false }
}
async function setSlot(slot: Slot) {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`node-management/slots/${slot.id}/enabled`, 'POST', { enabled: !slot.enabled })
    await load()
    notice.value = `槽位「${slot.slotName}」已${slot.enabled ? '禁用' : '启用'}。`
  } catch (e) { error.value = e instanceof Error ? e.message : '槽位操作失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>节点管理</span><button class="action-btn" @click="load">刷新</button></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <div class="robot-grid"><RobotStatusCard v-for="node in nodes" :key="node.id" :name="node.name" :status="node.status" :network-zone="node.networkZone" :available-slots="node.reportedAvailableSlots" :cpu-usage="node.cpuUsage" :memory-mi-b="node.memoryUsage" /></div>
    <div class="table-wrap"><table><thead><tr><th>节点</th><th>环境与能力</th><th>Worker 槽位</th><th>状态</th><th>操作</th></tr></thead>
      <tbody><tr v-for="node in nodes" :key="node.id">
        <td><button class="action-btn" @click="selectedNodeId = node.id">{{ node.name }} · 查看详情</button><small>{{ node.id }}</small><small>最后心跳：{{ node.lastHeartbeatAt ? new Date(node.lastHeartbeatAt).toLocaleString('zh-CN') : '未连接' }}</small></td>
        <td>{{ node.nodeKind }} · {{ node.osPlatform }}<small>{{ node.networkZone || '默认网络区' }} / {{ node.capabilities.map(c => c.code).join('、') || '无能力' }}</small><small>Agent CPU：{{ node.cpuUsage === null ? '未上报' : `${node.cpuUsage}%` }} · 进程内存：{{ node.memoryUsage === null ? '未上报' : `${node.memoryUsage} MiB` }} · 上报可用槽位：{{ node.reportedAvailableSlots ?? '未上报' }}</small></td>
        <td><div v-for="slot in slots.filter(x => x.nodeId === node.id)" :key="slot.id">{{ slot.slotName }} · {{ slot.executionId ? '执行中' : slot.enabled ? '空闲' : '禁用' }}
          <button class="action-btn" :disabled="busy || !!slot.executionId" @click="setSlot(slot)">{{ slot.enabled ? '禁用' : '启用' }}</button></div></td>
        <td><StatusBadge :label="node.status" :tone="node.status === 'Online' ? 'success' : 'warning'" /></td>
        <td><button v-if="node.status === 'PendingApproval'" class="action-btn" :disabled="busy" @click="change(node, 'approve')">批准</button>
          <button v-if="node.status === 'PendingApproval'" class="action-btn" :disabled="busy" @click="change(node, 'reject')">拒绝</button>
          <button v-if="node.status === 'Online'" class="action-btn" :disabled="busy" @click="change(node, 'drain')">排空</button>
          <button v-if="['Draining', 'Disabled', 'Offline'].includes(node.status)" class="action-btn" :disabled="busy" @click="change(node, 'restore')">恢复</button>
          <button v-if="node.status !== 'Revoked' && !slots.some(x => x.nodeId === node.id && !!x.executionId)" class="action-btn" :disabled="busy" @click="change(node, 'revoke')">吊销</button></td>
      </tr></tbody></table><p v-if="nodes.length === 0" class="muted empty">暂无节点，启动 NodeAgent 并等待注册申请。</p></div>
  </section>
  <DetailDrawer :open="!!selectedNodeId" title="节点详情" @close="selectedNodeId = ''"><NodeDetailPage v-if="selectedNodeId" :key="selectedNodeId" :token="token" :node-id="selectedNodeId" @back="selectedNodeId = ''" /></DetailDrawer>
</template>
