<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Detail = { id: string; name: string; status: string; nodeKind: string; osPlatform: string; architecture: string;
  networkZone?: string | null; agentVersion?: string | null; lastHeartbeatAt?: string | null;
  cpuUsage: number | null; memoryUsage: number | null; reportedAvailableSlots: number | null;
  capabilities: Array<{ id: string; code: string; version?: string | null; enabled: boolean }>;
  slots: Array<{ id: string; slotName: string; enabled: boolean; executionId?: string | null; leaseExpiresAt?: string | null }>;
  executionCounts: Array<{ status: string; count: number }>;
  recentExecutions: Array<{ id: string; taskItemId: string; status: string; createdAt: string; error?: string | null }> }
const props = defineProps<{ token: string; nodeId: string }>()
defineEmits<{ back: [] }>()
const detail = ref<Detail | null>(null)
const error = ref('')
const busy = ref(false)
async function load() {
  busy.value = true; error.value = ''
  try {
    const response = await fetch(`/api/node-management/nodes/${props.nodeId}`, { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`节点详情加载失败 (${response.status})`)
    detail.value = await response.json() as Detail
  } catch (e) { error.value = e instanceof Error ? e.message : '节点详情加载失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>节点详情 · {{ detail?.name || nodeId }}</span><div><button class="action-btn" @click="$emit('back')">返回节点列表</button><button class="action-btn" :disabled="busy" @click="load">刷新</button></div></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <template v-if="detail">
      <p class="muted">{{ detail.status }} · {{ detail.nodeKind }} · {{ detail.osPlatform }} / {{ detail.architecture }} · 网络区：{{ detail.networkZone || '默认' }}</p>
      <p class="muted">Agent 版本：{{ detail.agentVersion || '未上报' }} · 最后心跳：{{ detail.lastHeartbeatAt ? new Date(detail.lastHeartbeatAt).toLocaleString('zh-CN') : '未连接' }}</p>
      <p class="muted">Agent 进程 CPU：{{ detail.cpuUsage === null ? '未上报' : `${detail.cpuUsage}%` }} · 进程常驻内存：{{ detail.memoryUsage === null ? '未上报' : `${detail.memoryUsage} MiB` }} · 心跳可用槽位：{{ detail.reportedAvailableSlots ?? '未上报' }}</p>
      <h3>能力</h3>
      <div class="table-wrap"><table><thead><tr><th>能力</th><th>版本</th><th>状态</th></tr></thead><tbody><tr v-for="capability in detail.capabilities" :key="capability.id"><td>{{ capability.code }}</td><td>{{ capability.version || '—' }}</td><td>{{ capability.enabled ? '启用' : '禁用' }}</td></tr></tbody></table><p v-if="!detail.capabilities.length" class="muted empty">暂无能力。</p></div>
      <h3>WorkerSlot</h3>
      <div class="table-wrap"><table><thead><tr><th>槽位</th><th>状态</th><th>执行 ID</th><th>租约到期</th></tr></thead><tbody><tr v-for="slot in detail.slots" :key="slot.id"><td>{{ slot.slotName }}</td><td>{{ slot.executionId ? '执行中' : slot.enabled ? '空闲' : '禁用' }}</td><td>{{ slot.executionId || '—' }}</td><td>{{ slot.leaseExpiresAt ? new Date(slot.leaseExpiresAt).toLocaleString('zh-CN') : '—' }}</td></tr></tbody></table><p v-if="!detail.slots.length" class="muted empty">暂无槽位。</p></div>
      <h3>执行状态统计</h3>
      <div class="table-wrap"><table><thead><tr><th>状态</th><th>数量</th></tr></thead><tbody><tr v-for="item in detail.executionCounts" :key="item.status"><td>{{ item.status }}</td><td>{{ item.count }}</td></tr></tbody></table><p v-if="!detail.executionCounts.length" class="muted empty">暂无执行。</p></div>
      <h3>最近 20 次节点执行</h3>
      <div class="table-wrap"><table><thead><tr><th>创建时间</th><th>执行 ID</th><th>状态</th><th>错误</th></tr></thead><tbody><tr v-for="execution in detail.recentExecutions" :key="execution.id"><td>{{ new Date(execution.createdAt).toLocaleString('zh-CN') }}</td><td>{{ execution.id }}</td><td>{{ execution.status }}</td><td>{{ execution.error || '—' }}</td></tr></tbody></table><p v-if="!detail.recentExecutions.length" class="muted empty">暂无节点执行记录。</p></div>
    </template>
  </section>
</template>
