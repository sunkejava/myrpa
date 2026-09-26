<script setup lang="ts">
import type { ExecutionEntry } from '../../types/task'
defineProps<{ logs: ExecutionEntry[] }>()
</script>

<template>
  <section><h3>执行日志</h3><div class="table-wrap"><table><thead><tr><th>序号</th><th>级别</th><th>步骤</th><th>内容</th></tr></thead><tbody><tr v-for="entry in logs" :key="entry.id"><td>{{ entry.sequence }}</td><td>{{ entry.level }}</td><td>{{ entry.stepId || '—' }}</td><td>{{ entry.sensitive ? '敏感信息已隐藏' : entry.message }}</td></tr></tbody></table><p v-if="!logs.length" class="muted empty">暂无执行日志。</p></div></section>
</template>
