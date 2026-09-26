<script setup lang="ts">
import type { TaskItem } from '../../types/task'
defineProps<{ items: TaskItem[] }>()
defineEmits<{ inspect: [executionId: string] }>()
</script>

<template>
  <div class="table-wrap"><table><thead><tr><th>序号</th><th>状态</th><th>重试次数</th><th>执行实例</th></tr></thead><tbody>
    <tr v-for="item in items" :key="item.id"><td>{{ item.sequence }}</td><td>{{ item.status }}</td><td>{{ item.retryCount }}</td><td><div v-for="execution in item.executions" :key="execution.id"><button type="button" class="action-btn" @click="$emit('inspect', execution.id)">{{ execution.id.slice(0, 8) }} · {{ execution.status }}</button><small v-if="execution.error">{{ execution.error }}</small></div></td></tr>
  </tbody></table></div>
</template>
