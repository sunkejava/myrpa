<script setup lang="ts">
import StatusBadge from '../common/StatusBadge.vue'

defineProps<{ name: string; status: string; networkZone?: string | null; availableSlots?: number | null; cpuUsage?: number | null; memoryMiB?: number | null }>()
</script>

<template>
  <article class="robot-card"><div><span class="eyebrow">NODE AGENT</span><h3>{{ name }}</h3></div><StatusBadge :label="status" :tone="status === 'Online' ? 'success' : 'warning'" />
    <p class="muted">{{ networkZone || '默认网络区' }} · 可用槽位 {{ availableSlots ?? '未上报' }}</p>
    <p class="muted">进程 CPU {{ cpuUsage === null || cpuUsage === undefined ? '未上报' : `${cpuUsage}%` }} · 内存 {{ memoryMiB === null || memoryMiB === undefined ? '未上报' : `${memoryMiB} MiB` }}</p>
  </article>
</template>
