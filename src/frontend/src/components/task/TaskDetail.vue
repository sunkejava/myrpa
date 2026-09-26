<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Execution = { id: string; taskItemId: string; status: string; error?: string | null }
type Item = { id: string; sequence: number; status: string; retryCount: number; resultJson?: string | null; executions: Execution[] }
type Task = { id: string; name: string; status: string; approvalStatus?: string | null; items: Item[] }
type Checkpoint = { stepId: string; sequence: number; eventType: string; metadataJson?: string | null }
type Log = { id: string; sequence: number; level: string; eventType: string; stepId?: string; message: string; sensitive: boolean }
type Artifact = { id: string; fileName: string; artifactType: string; size: number; sha256?: string | null; expiresAt?: string | null }
type Timeline = { status: string; nodeId?: string | null; workerSlotId?: string | null; lease?: { released: boolean; expiresAt: string; lastHeartbeatAt: string } | null; events: Array<{ sequence: number; eventType: string; stepId?: string | null; message: string; createdAt: string }> }
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
    <div v-if="task" class="table-wrap"><table><thead><tr><th>序号</th><th>状态</th><th>重试次数</th><th>执行实例</th></tr></thead><tbody>
      <tr v-for="item in task.items" :key="item.id"><td>{{ item.sequence }}</td><td>{{ item.status }}</td><td>{{ item.retryCount }}</td><td><div v-for="execution in item.executions" :key="execution.id"><button type="button" class="action-btn" @click="chooseExecution(execution.id)">{{ execution.id.slice(0, 8) }} · {{ execution.status }}</button><small v-if="execution.error">{{ execution.error }}</small></div></td></tr>
    </tbody></table></div>
    <template v-if="executionId">
      <h3>调度与执行时间线</h3>
      <template v-if="timeline">
        <p class="muted">状态：{{ timeline.status }} · 节点：{{ timeline.nodeId || '未分配' }} · WorkerSlot：{{ timeline.workerSlotId || '未分配' }}</p>
        <p class="muted">租约：{{ timeline.lease ? (timeline.lease.released ? '已释放' : new Date(timeline.lease.expiresAt) > new Date() ? '生效中' : '已过期') : '未建立' }}</p>
        <div class="table-wrap"><table><thead><tr><th>时间</th><th>序号</th><th>事件</th><th>步骤</th><th>内容</th></tr></thead><tbody><tr v-for="entry in timeline.events" :key="entry.sequence"><td>{{ new Date(entry.createdAt).toLocaleString('zh-CN') }}</td><td>{{ entry.sequence }}</td><td>{{ entry.eventType }}</td><td>{{ entry.stepId || '—' }}</td><td>{{ entry.message }}</td></tr></tbody></table><p v-if="!timeline.events.length" class="muted empty">暂无执行事件。</p></div>
      </template>
      <h3>Step 检查点</h3>
      <p class="muted">只有“开始”而没有“完成”的提交步骤须先核验外部系统状态，不能直接重跑。</p>
      <div class="table-wrap"><table><thead><tr><th>序号</th><th>Step ID</th><th>类型</th><th>事件</th></tr></thead><tbody><tr v-for="item in checkpoints" :key="item.sequence"><td>{{ item.sequence }}</td><td>{{ item.stepId }}</td><td>{{ stepType(item.metadataJson) }}</td><td>{{ item.eventType === 'StepStarted' ? '开始' : '完成' }}</td></tr></tbody></table><p v-if="!checkpoints.length" class="muted empty">暂无 Step 检查点。</p></div>
      <h3>执行日志</h3>
      <div class="table-wrap"><table><thead><tr><th>序号</th><th>级别</th><th>步骤</th><th>内容</th></tr></thead><tbody><tr v-for="entry in logs" :key="entry.id"><td>{{ entry.sequence }}</td><td>{{ entry.level }}</td><td>{{ entry.stepId || '—' }}</td><td>{{ entry.sensitive ? '敏感信息已隐藏' : entry.message }}</td></tr></tbody></table><p v-if="!logs.length" class="muted empty">暂无执行日志。</p></div>
      <h3>执行产物</h3>
      <div class="table-wrap"><table><thead><tr><th>文件</th><th>类型</th><th>大小</th><th>有效期</th><th>操作</th></tr></thead><tbody><tr v-for="artifact in artifacts" :key="artifact.id"><td>{{ artifact.fileName }}<small v-if="artifact.sha256">SHA256：{{ artifact.sha256 }}</small></td><td>{{ artifact.artifactType }}</td><td>{{ artifact.size }} B</td><td>{{ artifact.expiresAt ? new Date(artifact.expiresAt).toLocaleString('zh-CN') : '长期' }}</td><td><button class="action-btn" @click="download(artifact)">下载</button></td></tr></tbody></table><p v-if="!artifacts.length" class="muted empty">暂无执行产物。</p></div>
    </template>
  </section>
</template>
