<script setup lang="ts">
import { onMounted, ref } from 'vue'
import TaskTimeline from './TaskTimeline.vue'
import ExecutionLog from './ExecutionLog.vue'
import TaskItemTable from './TaskItemTable.vue'
import type { TaskItem as Item, Timeline, ExecutionEntry as Log } from '../../types/task'

type Task = { id: string; name: string; status: string; approvalStatus?: string | null; items: Item[] }
type Checkpoint = { stepId: string; sequence: number; eventType: string; metadataJson?: string | null }
type Artifact = { id: string; fileName: string; artifactType: string; size: number; sha256?: string | null; expiresAt?: string | null }
const props = defineProps<{ taskId: string; token: string }>()
const task = ref<Task | null>(null)
const checkpoints = ref<Checkpoint[]>([])
const logs = ref<Log[]>([])
const artifacts = ref<Artifact[]>([])
const timeline = ref<Timeline | null>(null)
const executionId = ref('')
const error = ref('')
const busy = ref(false)

async function get<T>(path: string): Promise<T> {
  const response = await fetch(`/api/${path}`, { headers: { Authorization: `Bearer ${props.token}` } })
  if (!response.ok) throw new Error(`执行详情请求失败 (${response.status})`)
  return await response.json() as T
}
async function load() {
  busy.value = true; error.value = ''
  try { task.value = await get<Task>(`tasks/${props.taskId}`) }
  catch (e) { error.value = e instanceof Error ? e.message : '加载失败' }
  finally { busy.value = false }
}
async function chooseExecution(id: string) {
  executionId.value = id; error.value = ''; timeline.value = null; checkpoints.value = []; logs.value = []; artifacts.value = []
  try {
    const [nextCheckpoints, nextLogs, nextArtifacts, nextTimeline] = await Promise.all([
      get<Checkpoint[]>(`executions/${id}/checkpoints`), get<Log[]>(`executions/${id}/logs?limit=200`),
      get<Artifact[]>(`executions/${id}/artifacts`), get<Timeline>(`executions/${id}/timeline`)
    ])
    if (executionId.value === id) {
      checkpoints.value = nextCheckpoints; logs.value = nextLogs; artifacts.value = nextArtifacts; timeline.value = nextTimeline
    }
  } catch (e) { error.value = e instanceof Error ? e.message : '读取执行日志失败' }
}
async function download(artifact: Artifact) {
  error.value = ''
  try {
    const response = await fetch(`/api/executions/${executionId.value}/artifacts/${artifact.id}/content`,
      { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`产物下载失败 (${response.status})`)
    const url = URL.createObjectURL(await response.blob())
    const link = document.createElement('a')
    link.href = url; link.download = artifact.fileName; document.body.append(link); link.click(); link.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 60_000)
  } catch (e) { error.value = e instanceof Error ? e.message : '产物下载失败' }
}
function stepType(json?: string | null): string {
  try { return String((JSON.parse(json || '{}') as { stepType?: string }).stepType || '') }
  catch { return '' }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>任务详情 · {{ task?.name || taskId }}</span><button class="action-btn" :disabled="busy" @click="load">刷新</button></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <p v-if="task" class="muted">状态：{{ task.status }}{{ task.approvalStatus ? ` · 审批：${task.approvalStatus}` : '' }}</p>
    <TaskItemTable v-if="task" :items="task.items" @inspect="chooseExecution" />
    <template v-if="executionId">
      <TaskTimeline :timeline="timeline" />
      <h3>Step 检查点</h3>
      <p class="muted">只有“开始”而没有“完成”的提交步骤须先核验外部系统状态，不能直接重跑。</p>
      <div class="table-wrap"><table><thead><tr><th>序号</th><th>Step ID</th><th>类型</th><th>事件</th></tr></thead><tbody><tr v-for="item in checkpoints" :key="item.sequence"><td>{{ item.sequence }}</td><td>{{ item.stepId }}</td><td>{{ stepType(item.metadataJson) }}</td><td>{{ item.eventType === 'StepStarted' ? '开始' : '完成' }}</td></tr></tbody></table><p v-if="!checkpoints.length" class="muted empty">暂无 Step 检查点。</p></div>
      <ExecutionLog :logs="logs" />
      <h3>执行产物</h3>
      <div class="table-wrap"><table><thead><tr><th>文件</th><th>类型</th><th>大小</th><th>有效期</th><th>操作</th></tr></thead><tbody><tr v-for="artifact in artifacts" :key="artifact.id"><td>{{ artifact.fileName }}<small v-if="artifact.sha256">SHA256：{{ artifact.sha256 }}</small></td><td>{{ artifact.artifactType }}</td><td>{{ artifact.size }} B</td><td>{{ artifact.expiresAt ? new Date(artifact.expiresAt).toLocaleString('zh-CN') : '长期' }}</td><td><button class="action-btn" @click="download(artifact)">下载</button></td></tr></tbody></table><p v-if="!artifacts.length" class="muted empty">暂无执行产物。</p></div>
    </template>
  </section>
</template>
