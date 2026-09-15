<script setup lang="ts">
import StatusBadge from '../common/StatusBadge.vue'

type TaskRow = { id: string; name: string; progress: number; status: string; success: number; failed: number }
defineProps<{ tasks: TaskRow[] }>()
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>任务队列</span><small>Task / TaskItem / Execution</small></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>任务</th><th>进度</th><th>成功</th><th>失败</th><th>状态</th></tr></thead>
        <tbody>
          <tr v-for="task in tasks" :key="task.id">
            <td><strong>{{ task.name }}</strong><small>{{ task.id }}</small></td>
            <td><div class="progress"><i :style="{ width: `${task.progress}%` }"></i></div><small>{{ task.progress }}%</small></td>
            <td>{{ task.success }}</td><td>{{ task.failed }}</td>
            <td><StatusBadge :label="task.status" :tone="task.status === 'Running' ? 'info' : task.status === 'Succeeded' ? 'success' : 'warning'" /></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
