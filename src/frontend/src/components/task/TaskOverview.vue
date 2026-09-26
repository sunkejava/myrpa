<script setup lang="ts">
import StatusBadge from '../common/StatusBadge.vue'
import TaskProgress from './TaskProgress.vue'
import { useLocale } from '../../locales'
const { t } = useLocale()

type TaskRow = { id: string; name: string; status: string; approvalStatus?: string | null; total: number; succeeded: number; failed: number; progress: number }
defineProps<{ tasks: TaskRow[]; queue: (id: string) => Promise<void>; retry: (id: string) => Promise<void>; cancel: (id: string) => Promise<void>; inspect: (id: string) => void }>()
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>{{ t('task.queue') }}</span><small>Task / TaskItem / Execution</small></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>{{ t('task.task') }}</th><th>{{ t('task.progress') }}</th><th>{{ t('task.successTotal') }}</th><th>{{ t('task.failed') }}</th><th>{{ t('task.status') }}</th><th>{{ t('task.actions') }}</th></tr></thead>
        <tbody>
          <tr v-for="task in tasks" :key="task.id">
            <td><strong>{{ task.name }}</strong><small>{{ task.id }}</small></td>
            <td><TaskProgress :succeeded="task.succeeded" :failed="task.failed" :total="task.total" /></td>
            <td>{{ task.succeeded }} / {{ task.total }}</td><td>{{ task.failed }}</td>
            <td><StatusBadge :label="task.approvalStatus === 'Pending' ? t('task.approvalPending') : task.approvalStatus === 'Rejected' ? t('task.approvalRejected') : task.status" :tone="task.status === 'Running' ? 'info' : task.status === 'Succeeded' ? 'success' : 'warning'" /></td>
            <td><button class="action-btn" @click="inspect(task.id)">{{ t('task.details') }}</button><button v-if="task.status === 'Draft' && task.approvalStatus !== 'Pending' && task.approvalStatus !== 'Rejected'" class="action-btn" @click="queue(task.id)">{{ t('task.enqueue') }}</button><button v-if="task.status === 'Failed' && task.failed" class="action-btn" @click="retry(task.id)">{{ t('task.retry') }}</button><button v-if="!['Succeeded', 'Failed', 'Cancelled'].includes(task.status)" class="action-btn" @click="cancel(task.id)">{{ t('task.cancel') }}</button></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
