<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import TaskOverview from '../components/task/TaskOverview.vue'
import TaskDetail from '../components/task/TaskDetail.vue'
import { useLocale } from '../locales'
import { listTasks, taskAction, type TaskListItem as Task } from '../api/modules/tasks'

const props = defineProps<{ token: string }>()
const emit = defineEmits<{ intervention: [executionId: string] }>()
const tasks = ref<Task[]>([])
const selectedTaskId = ref('')
const error = ref('')
const { t } = useLocale()
const taskRows = computed(() => tasks.value.map(task => ({ ...task, progress: task.total ? Math.round(100 * (task.succeeded + task.failed) / task.total) : 0 })))

async function loadTasks() {
  try { tasks.value = await listTasks(props.token) }
  catch (e) { error.value = e instanceof Error ? e.message : '任务列表加载失败' }
}
async function perform(id: string, operation: string, fallback: string) {
  error.value = ''
  try { await taskAction(props.token, id, operation); await loadTasks() }
  catch (e) { error.value = e instanceof Error ? e.message : fallback }
}
const queueTask = (id: string) => perform(id, 'queue', '入队失败')
const retryTask = (id: string) => perform(id, 'retry-failed', '重试失败')
async function cancelTask(id: string) {
  if (!window.confirm(t('task.confirmCancel'))) return
  await perform(id, 'cancel', '取消任务失败')
}
onMounted(loadTasks)
</script>

<template>
  <p v-if="error" class="error" role="alert">{{ error }}</p>
  <template v-if="selectedTaskId">
    <div class="page-toolbar"><button class="action-btn" @click="selectedTaskId = ''">{{ t('task.backToList') }}</button><span class="muted">{{ t('task.details') }}</span></div>
    <TaskDetail :key="selectedTaskId" :task-id="selectedTaskId" :token="token" @intervention="emit('intervention', $event)" />
  </template>
  <template v-else>
    <div class="page-toolbar"><div><h2>{{ t('task.myTasks') }}</h2><p class="muted">{{ t('task.queue') }}</p></div><button class="action-btn" @click="loadTasks">{{ t('common.refresh') }}</button></div>
    <TaskOverview :tasks="taskRows" :queue="queueTask" :retry="retryTask" :cancel="cancelTask" :inspect="id => selectedTaskId = id" />
    <p v-if="tasks.length === 0" class="muted empty">{{ t('task.empty') }}</p>
  </template>
</template>
