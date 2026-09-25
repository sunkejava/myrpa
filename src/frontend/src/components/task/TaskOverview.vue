<script setup lang="ts">
import StatusBadge from '../common/StatusBadge.vue'

type TaskRow = { id: string; name: string; status: string; approvalStatus?: string | null; total: number; succeeded: number; failed: number; progress: number }
defineProps<{ tasks: TaskRow[]; queue: (id: string) => Promise<void> }>()
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>任务队列</span><small>Task / TaskItem / Execution</small></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>任务</th><th>进度</th><th>成功 / 总数</th><th>失败</th><th>状态</th><th>操作</th></tr></thead>
        <tbody>
          <tr v-for="task in tasks" :key="task.id">
            <td><strong>{{ task.name }}</strong><small>{{ task.id }}</small></td>
            <td><div class="progress"><i :style="{ width: `${task.progress}%` }"></i></div><small>{{ task.progress }}%</small></td>
            <td>{{ task.succeeded }} / {{ task.total }}</td><td>{{ task.failed }}</td>
            <td><StatusBadge :label="task.approvalStatus === 'Pending' ? '待审批' : task.approvalStatus === 'Rejected' ? '审批拒绝' : task.status" :tone="task.status === 'Running' ? 'info' : task.status === 'Succeeded' ? 'success' : 'warning'" /></td>
            <td><button v-if="task.status === 'Draft' && task.approvalStatus !== 'Pending' && task.approvalStatus !== 'Rejected'" class="action-btn" @click="queue(task.id)">入队执行</button></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
