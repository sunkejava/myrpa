<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import TaskOverview from '../components/task/TaskOverview.vue'
import TaskDetail from '../components/task/TaskDetail.vue'
import { useLocale } from '../locales'

type Task = { id: string; name: string; status: string; approvalStatus?: string | null; total: number; succeeded: number; failed: number }
const props = defineProps<{ token: string }>()
const tasks = ref<Task[]>([])
const selectedTaskId = ref('')
const error = ref('')
const { t } = useLocale()
const taskRows = computed(() => tasks.value.map(task => ({ ...task, progress: task.total ? Math.round(100 * (task.succeeded + task.failed) / task.total) : 0 })))

async function call<T>(path: string, method = 'GET'): Promise<T> {
  const response = await fetch(`/api/tasks${path}`, { method, headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' },
    ...(method === 'POST' ? { body: '{}' } : {}) })
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null)
    throw new Error(body && typeof body === 'object' && 'message' in body ? String(body.message) : `请求失败 (${response.status})`)
  }
  const content = await response.text()
  return (content ? JSON.parse(content) : undefined) as T
}
async function loadTasks() {
  try { tasks.value = await call<Task[]>('') }
  catch (e) { error.value = e instanceof Error ? e.message : '任务列表加载失败' }
}
async function perform(id: string, operation: string, fallback: string) {
  error.value = ''
  try { await call(`/${id}/${operation}`, 'POST'); await loadTasks() }
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
  <div class="panel-title"><span>{{ t('task.myTasks') }}</span><button class="action-btn" @click="loadTasks">{{ t('common.refresh') }}</button></div>
  <TaskOverview :tasks="taskRows" :queue="queueTask" :retry="retryTask" :cancel="cancelTask" :inspect="id => selectedTaskId = id" />
  <TaskDetail v-if="selectedTaskId" :key="selectedTaskId" :task-id="selectedTaskId" :token="token" />
  <p v-if="tasks.length === 0" class="muted empty">{{ t('task.empty') }}</p>
</template>
