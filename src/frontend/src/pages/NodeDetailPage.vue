<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import DataTable from '../components/table/DataTable.vue'
import { nodeRequest, type ManagedNodeDetail } from '../api/modules/nodes'
import { useLocale } from '../locales'

const props = defineProps<{ token: string; nodeId: string }>()
const emit = defineEmits<{ back: [] }>()
const { t } = useLocale()
const detail = ref<ManagedNodeDetail | null>(null)
const tab = ref<'overview' | 'capabilities' | 'slots' | 'executions'>('overview')
const error = ref('')
const busy = ref(false)
const capabilities = computed(() => [{ key: 'code', label: t('nodes.capabilities'), sortable: true, filterable: true },
  { key: 'version', label: t('nodes.version') }, { key: 'enabled', label: t('nodes.status'), format: (value: unknown) => t(value ? 'nodes.enabled' : 'nodes.disabled') }])
const slots = computed(() => [{ key: 'slotName', label: t('nodes.slots'), sortable: true, filterable: true },
  { key: 'executionId', label: t('nodes.executionId') },
  { key: 'leaseExpiresAt', label: t('nodes.lease'), format: (value: unknown) => value ? new Date(String(value)).toLocaleString() : '—' }])
const executions = computed(() => [{ key: 'createdAt', label: t('nodes.createdAt'), sortable: true, format: (value: unknown) => new Date(String(value)).toLocaleString() },
  { key: 'id', label: t('nodes.executionId') }, { key: 'status', label: t('nodes.status') }, { key: 'error', label: t('nodes.error') }])
const counts = computed(() => [{ key: 'status', label: t('nodes.status') }, { key: 'count', label: t('nodes.count') }])
async function load() {
  busy.value = true; error.value = ''
  try { detail.value = await nodeRequest<ManagedNodeDetail>(props.token, `node-management/nodes/${props.nodeId}`) }
  catch (e) { error.value = e instanceof Error ? e.message : t('nodes.detailFailed') }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel node-detail">
    <div class="panel-title"><h3>{{ t('nodes.details') }} · {{ detail?.name || nodeId }}</h3><div class="actions"><button class="action-btn" @click="emit('back')">{{ t('nodes.back') }}</button><button class="action-btn" :disabled="busy" @click="load">{{ t('common.refresh') }}</button></div></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <template v-if="detail">
      <div class="page-tabs" role="tablist" :aria-label="t('nodes.details')">
        <button v-for="item in (['overview', 'capabilities', 'slots', 'executions'] as const)" :key="item" class="action-btn" role="tab" :aria-selected="tab === item" @click="tab = item">{{ t(`nodes.${item}`) }}</button>
      </div>
      <dl v-if="tab === 'overview'" class="node-facts">
        <div><dt>{{ t('nodes.status') }}</dt><dd>{{ detail.status }}</dd></div><div><dt>{{ t('nodes.environment') }}</dt><dd>{{ detail.nodeKind }} · {{ detail.osPlatform }} / {{ detail.architecture }}</dd></div>
        <div><dt>{{ t('nodes.networkZone') }}</dt><dd>{{ detail.networkZone || t('nodes.defaultZone') }}</dd></div><div><dt>{{ t('nodes.agentVersion') }}</dt><dd>{{ detail.agentVersion || t('nodes.unreported') }}</dd></div>
        <div><dt>{{ t('nodes.lastHeartbeat') }}</dt><dd>{{ detail.lastHeartbeatAt ? new Date(detail.lastHeartbeatAt).toLocaleString() : t('nodes.disconnected') }}</dd></div>
        <div><dt>{{ t('nodes.cpu') }}</dt><dd>{{ detail.cpuUsage === null ? t('nodes.unreported') : `${detail.cpuUsage}%` }}</dd></div>
        <div><dt>{{ t('nodes.memory') }}</dt><dd>{{ detail.memoryUsage === null ? t('nodes.unreported') : `${detail.memoryUsage} MiB` }}</dd></div>
        <div><dt>{{ t('nodes.availableSlots') }}</dt><dd>{{ detail.reportedAvailableSlots ?? t('nodes.unreported') }}</dd></div>
      </dl>
      <DataTable v-else-if="tab === 'capabilities'" :rows="detail.capabilities" :columns="capabilities" :empty-label="t('nodes.emptyCapabilities')" filename="node-capabilities.csv" @refresh="load" />
      <DataTable v-else-if="tab === 'slots'" :rows="detail.slots" :columns="slots" :empty-label="t('nodes.emptySlots')" filename="node-slots.csv" @refresh="load" />
      <template v-else><h3 class="node-section-title">{{ t('nodes.counts') }}</h3><DataTable :rows="detail.executionCounts" :columns="counts" row-key="status" :empty-label="t('nodes.emptyExecutions')" filename="execution-counts.csv" @refresh="load" />
        <h3 class="node-section-title">{{ t('nodes.recent') }}</h3><DataTable :rows="detail.recentExecutions" :columns="executions" :empty-label="t('nodes.emptyExecutions')" filename="node-executions.csv" @refresh="load" /></template>
    </template>
  </section>
</template>
