<script setup lang="ts">
import type { Timeline } from '../../types/task'
defineProps<{ timeline: Timeline | null }>()
</script>

<template>
  <section>
    <h3>调度与执行时间线</h3>
    <template v-if="timeline">
      <p class="muted">状态：{{ timeline.status }} · 节点：{{ timeline.nodeId || '未分配' }} · WorkerSlot：{{ timeline.workerSlotId || '未分配' }}</p>
      <p class="muted">租约：{{ timeline.lease ? (timeline.lease.released ? '已释放' : new Date(timeline.lease.expiresAt) > new Date() ? '生效中' : '已过期') : '未建立' }}</p>
      <div class="table-wrap"><table><thead><tr><th>时间</th><th>序号</th><th>事件</th><th>步骤</th><th>内容</th></tr></thead><tbody><tr v-for="entry in timeline.events" :key="entry.sequence"><td>{{ new Date(entry.createdAt).toLocaleString('zh-CN') }}</td><td>{{ entry.sequence }}</td><td>{{ entry.eventType }}</td><td>{{ entry.stepId || '—' }}</td><td>{{ entry.message }}</td></tr></tbody></table><p v-if="!timeline.events.length" class="muted empty">暂无执行事件。</p></div>
    </template>
  </section>
</template>
