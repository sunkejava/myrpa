<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import StatusBadge from '../components/common/StatusBadge.vue'
import NodeDetailPage from './NodeDetailPage.vue'
import RobotStatusCard from '../components/robot/RobotStatusCard.vue'
import DetailDrawer from '../components/common/DetailDrawer.vue'
import DataTable from '../components/table/DataTable.vue'
import ConfirmAction from '../components/common/ConfirmAction.vue'
import { nodeRequest, type ManagedNode, type WorkerSlot } from '../api/modules/nodes'
import { useLocale } from '../locales'

const props = defineProps<{ token: string }>()
const { t } = useLocale()
const tab = ref<'nodes' | 'slots'>('nodes')
const nodes = ref<ManagedNode[]>([])
const slots = ref<WorkerSlot[]>([])
const busy = ref(false)
const error = ref('')
const notice = ref('')
const selectedNodeId = ref('')
const nodeColumns = computed(() => [
  { key: 'name', label: t('nodes.name'), sortable: true, filterable: true },
  { key: 'nodeKind', label: t('nodes.environment'), format: (value: unknown, row: object) => `${value} · ${(row as ManagedNode).osPlatform}` },
  { key: 'status', label: t('nodes.status'), sortable: true, filterable: true },
  { key: 'reportedAvailableSlots', label: t('nodes.availableSlots'), sortable: true, format: (value: unknown) => value === null ? t('nodes.unreported') : String(value) }
])
const slotRows = computed(() => slots.value.map(slot => ({ ...slot, nodeName: nodes.value.find(node => node.id === slot.nodeId)?.name || slot.nodeId,
  state: slot.executionId ? t('nodes.pending') : slot.enabled ? t('nodes.idle') : t('nodes.disabled') })))
const slotColumns = computed(() => [
  { key: 'slotName', label: t('nodes.slots'), sortable: true, filterable: true },
  { key: 'nodeName', label: t('nodes.name'), sortable: true, filterable: true },
  { key: 'state', label: t('nodes.status'), sortable: true }
])
const call = <T,>(path: string, method = 'GET', body?: object) => nodeRequest<T>(props.token, path, method, body)
async function load() {
  error.value = ''
  try { [nodes.value, slots.value] = await Promise.all([call<ManagedNode[]>('node-management/nodes'), call<WorkerSlot[]>('node-management/slots')]) }
  catch (e) { error.value = e instanceof Error ? e.message : t('nodes.loadFailed') }
}
async function change(node: ManagedNode, action: 'approve' | 'reject' | 'revoke' | 'drain' | 'restore') {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`nodes/${node.id}/${action === 'restore' ? 'status' : action}`, 'POST', action === 'restore' ? { status: 'Online' } : undefined)
    await load()
    notice.value = t('nodes.nodeSuccess').replace('{name}', node.name)
  } catch (e) { error.value = e instanceof Error ? e.message : t('nodes.nodeFailed') }
  finally { busy.value = false }
}
async function setSlot(slot: WorkerSlot) {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`node-management/slots/${slot.id}/enabled`, 'POST', { enabled: !slot.enabled })
    await load()
    notice.value = t('nodes.slotSuccess').replace('{name}', slot.slotName).replace('{state}', t(slot.enabled ? 'nodes.disabled' : 'nodes.enabled'))
  } catch (e) { error.value = e instanceof Error ? e.message : t('nodes.slotFailed') }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <div class="page-toolbar"><h2>{{ t('nodes.title') }}</h2><button class="action-btn" @click="load">{{ t('common.refresh') }}</button></div>
  <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
  <div class="page-tabs" role="tablist" :aria-label="t('nodes.title')"><button role="tab" class="action-btn" :aria-selected="tab === 'nodes'" @click="tab = 'nodes'">{{ t('nodes.nodes') }}</button><button role="tab" class="action-btn" :aria-selected="tab === 'slots'" @click="tab = 'slots'">{{ t('nodes.slots') }}</button></div>
  <template v-if="tab === 'nodes'">
    <section class="panel"><DataTable :rows="nodes" :columns="nodeColumns" :loading="busy" :empty-label="t('nodes.empty')" filename="nodes.csv" @refresh="load">
      <template #cell-name="{ row }"><button class="action-btn" @click="selectedNodeId = (row as ManagedNode).id">{{ (row as ManagedNode).name }} · {{ t('nodes.details') }}</button><small>{{ (row as ManagedNode).id }}</small><small>{{ t('nodes.lastHeartbeat') }}：{{ (row as ManagedNode).lastHeartbeatAt ? new Date((row as ManagedNode).lastHeartbeatAt!).toLocaleString() : t('nodes.disconnected') }}</small></template>
      <template #cell-status="{ row }"><StatusBadge :label="(row as ManagedNode).status" :tone="(row as ManagedNode).status === 'Online' ? 'success' : 'warning'" /></template>
      <template #actions="{ row }"><div class="actions node-actions"><button v-if="(row as ManagedNode).status === 'PendingApproval'" class="action-btn" :disabled="busy" @click="change(row as ManagedNode, 'approve')">{{ t('nodes.approve') }}</button>
        <button v-if="(row as ManagedNode).status === 'PendingApproval'" class="action-btn" :disabled="busy" @click="change(row as ManagedNode, 'reject')">{{ t('nodes.reject') }}</button>
        <button v-if="(row as ManagedNode).status === 'Online'" class="action-btn" :disabled="busy" @click="change(row as ManagedNode, 'drain')">{{ t('nodes.drain') }}</button>
        <button v-if="['Draining', 'Disabled', 'Offline'].includes((row as ManagedNode).status)" class="action-btn" :disabled="busy" @click="change(row as ManagedNode, 'restore')">{{ t('nodes.restore') }}</button>
        <ConfirmAction v-if="(row as ManagedNode).status !== 'Revoked' && !slots.some(slot => slot.nodeId === (row as ManagedNode).id && !!slot.executionId)" :label="t('nodes.revoke')" :title="t('nodes.revoke')" :message="t('nodes.revokeConfirm').replace('{name}', (row as ManagedNode).name)" :disabled="busy" :busy="busy" @confirmed="change(row as ManagedNode, 'revoke')" /></div></template>
    </DataTable></section>
    <details v-if="nodes.length" class="panel node-status"><summary class="panel-title">{{ t('nodes.live') }}</summary><div class="robot-grid"><RobotStatusCard v-for="node in nodes" :key="node.id" :name="node.name" :status="node.status" :network-zone="node.networkZone" :available-slots="node.reportedAvailableSlots" :cpu-usage="node.cpuUsage" :memory-mi-b="node.memoryUsage" /></div></details>
  </template>
  <section v-else class="panel"><DataTable :rows="slotRows" :columns="slotColumns" :loading="busy" :empty-label="t('nodes.emptySlots')" filename="worker-slots.csv" @refresh="load"><template #actions="{ row }"><button class="action-btn" :disabled="busy || !!(row as WorkerSlot).executionId" @click="setSlot(row as WorkerSlot)">{{ (row as WorkerSlot).enabled ? t('nodes.disabled') : t('nodes.enabled') }}</button></template></DataTable></section>
  <DetailDrawer :open="!!selectedNodeId" :title="t('nodes.details')" @close="selectedNodeId = ''"><NodeDetailPage v-if="selectedNodeId" :key="selectedNodeId" :token="token" :node-id="selectedNodeId" @back="selectedNodeId = ''" /></DetailDrawer>
</template>
