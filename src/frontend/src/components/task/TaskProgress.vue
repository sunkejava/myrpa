<script setup lang="ts">
import { computed } from 'vue'
const props = defineProps<{ succeeded: number; failed: number; total: number }>()
const completed = computed(() => props.succeeded + props.failed)
const progress = computed(() => props.total ? Math.round(completed.value / props.total * 100) : 0)
</script>

<template>
  <div :aria-label="`任务进度 ${completed} / ${total}`" role="progressbar" :aria-valuenow="completed" aria-valuemin="0" :aria-valuemax="total">
    <div class="progress"><i :style="{ width: `${progress}%` }" /></div><small>{{ completed }} / {{ total }} · {{ progress }}%</small>
  </div>
</template>
