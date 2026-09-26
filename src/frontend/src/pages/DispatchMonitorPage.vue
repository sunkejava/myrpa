<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Count = { status: string; count: number }
type SlotCount = { enabled: boolean; busy: boolean; count: number }
type Entry = { id: string; taskId: string; taskName: string; itemSequence: number; nodeName?: string | null; nodeId?: string | null; workerSlotId?: string | null; status: string; error?: string | null; createdAt: string }
type Overview = { taskCounts: Count[]; executionCounts: Count[]; nodeCounts: Count[]; slotCounts: SlotCount[]; recent: Entry[] }
const props = defineProps<{ token: string }>()
const overview = ref<Overview | null>(null)
const error = ref('')
const busy = ref(false)
const count = (items: Count[], status: string) => items.find(x => x.status === status)?.count ?? 0
async function load() {
  busy.value = true; error.value = ''
  try {
    const response = await fetch('/api/dispatch-monitor?limit=30', { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`调度监控加载失败 (${response.status})`)
    overview.value = await response.json() as Overview
  } catch (e) { error.value = e instanceof Error ? e.message : '调度监控加载失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>调度监控</span><button class="action-btn" :disabled="busy" @click="load">刷新</button></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <template v-if="overview">
      <h3>当前任务与资源</h3>
      <div class="table-wrap"><table><thead><tr><th>待资源任务</th><th>排队任务</th><th>运行任务</th><th>失败任务</th><th>在线节点</th><th>忙碌槽位</th><th>可用槽位</th></tr></thead><tbody><tr>
        <td>{{ count(overview.taskCounts, 'WaitingForResource') }}</td><td>{{ count(overview.taskCounts, 'Queued') }}</td><td>{{ count(overview.taskCounts, 'Running') }}</td><td>{{ count(overview.taskCounts, 'Failed') }}</td>
        <td>{{ count(overview.nodeCounts, 'Online') }}</td><td>{{ overview.slotCounts.filter(x => x.busy).reduce((sum, x) => sum + x.count, 0) }}</td><td>{{ overview.slotCounts.filter(x => x.enabled && !x.busy).reduce((sum, x) => sum + x.count, 0) }}</td>
      </tr></tbody></table></div>
      <h3>执行状态</h3>
      <div class="table-wrap"><table><thead><tr><th>状态</th><th>数量</th></tr></thead><tbody><tr v-for="item in overview.executionCounts" :key="item.status"><td>{{ item.status }}</td><td>{{ item.count }}</td></tr></tbody></table><p v-if="!overview.executionCounts.length" class="muted empty">暂无执行记录。</p></div>
      <h3>最近 30 次执行</h3>
      <div class="table-wrap"><table><thead><tr><th>创建时间</th><th>任务 / 子项</th><th>节点</th><th>状态</th><th>错误</th></tr></thead><tbody><tr v-for="item in overview.recent" :key="item.id"><td>{{ new Date(item.createdAt).toLocaleString('zh-CN') }}</td><td>{{ item.taskName }} · {{ item.itemSequence }}<small>{{ item.id }}</small></td><td>{{ item.nodeName || item.nodeId || '未分配' }}<small v-if="item.workerSlotId">{{ item.workerSlotId }}</small></td><td>{{ item.status }}</td><td>{{ item.error || '—' }}</td></tr></tbody></table><p v-if="!overview.recent.length" class="muted empty">暂无执行记录。</p></div>
    </template>
  </section>
</template>
