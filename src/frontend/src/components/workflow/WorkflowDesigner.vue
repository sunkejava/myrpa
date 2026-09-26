<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import WorkflowCanvas from './WorkflowCanvas.vue'
import FormDialog from '../common/FormDialog.vue'
import { workflowRequest, type WorkflowResource as Resource, type WorkflowItem as Workflow, type WorkflowVersion as Version } from '../../api/modules/workflows'

const props = defineProps<{ token: string }>()
const cities = ref<Resource[]>([])
const systems = ref<Resource[]>([])
const functions = ref<Resource[]>([])
const workflows = ref<Workflow[]>([])
const versions = ref<Version[]>([])
const cityId = ref('')
const systemId = ref('')
const functionId = ref('')
const workflowId = ref('')
const workflowName = ref('')
const workflowDescription = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)
const creating = ref(false)
const definitionJson = ref(JSON.stringify({ version: 1, riskLevel: 'Low', requiresApproval: false, steps: [{ id: 'step-1', type: 'End' }] }, null, 2))
const stepTypes = ['Navigate', 'Click', 'Input', 'Select', 'Wait', 'WaitForElement', 'Extract', 'Upload', 'Download', 'Screenshot', 'Condition', 'Loop', 'HumanTask', 'Assert', 'End']
const currentWorkflow = computed(() => workflows.value.find(x => x.id === workflowId.value))
const parsedDefinition = computed(() => {
  try {
    const value: unknown = JSON.parse(definitionJson.value)
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null
  } catch { return null }
})
const steps = computed(() => Array.isArray(parsedDefinition.value?.steps) ? parsedDefinition.value.steps as { id?: string; type?: string; config?: Record<string, unknown> }[] : [])

const call = <T,>(path: string, method = 'GET', body?: object) => workflowRequest<T>(props.token, path, method, body)
function fail(e: unknown) { error.value = e instanceof Error ? e.message : '操作失败' }
async function loadWorkflows() { workflows.value = await call<Workflow[]>('workflows') }
async function chooseCity(id: string) {
  cityId.value = id; systemId.value = ''; functionId.value = ''; systems.value = []; functions.value = []
  try { systems.value = id ? await call<Resource[]>(`business-resources/cities/${id}/systems`) : [] }
  catch (e) { fail(e) }
}
async function chooseSystem(id: string) {
  systemId.value = id; functionId.value = ''; functions.value = []
  try { functions.value = id ? await call<Resource[]>(`business-resources/systems/${id}/functions`) : [] }
  catch (e) { fail(e) }
}
async function chooseWorkflow(id: string) {
  workflowId.value = id; versions.value = []
  try { versions.value = id ? await call<Version[]>(`workflows/${id}/versions`) : [] }
  catch (e) { fail(e) }
}
async function loadVersion(version: number) {
  try {
    const result = await call<{ definitionJson: string }>(`workflows/${workflowId.value}/versions/${version}`)
    definitionJson.value = JSON.stringify(JSON.parse(result.definitionJson), null, 2)
    notice.value = `已加载第 ${version} 版；修改后保存会创建新版本。`
  } catch (e) { fail(e) }
}
async function createWorkflow() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const result = await call<{ id: string }>('workflows', 'POST', {
      businessFunctionId: functionId.value, name: workflowName.value.trim(), description: workflowDescription.value.trim()
    })
    await loadWorkflows(); await chooseWorkflow(result.id)
    creating.value = false
    notice.value = 'Workflow 已创建。编辑定义后创建并发布版本。'
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
function updateDefinition(patch: Record<string, unknown>) {
  if (!parsedDefinition.value) { error.value = '定义 JSON 无效，请先修正内容。'; return }
  definitionJson.value = JSON.stringify({ ...parsedDefinition.value, ...patch }, null, 2)
}
function addStep(type: string) {
  if (!parsedDefinition.value) { error.value = '定义 JSON 无效，请先修正内容。'; return }
  const next = [...steps.value]
  const endIndex = next.findIndex(step => step.type?.toLowerCase() === 'end')
  next.splice(endIndex < 0 ? next.length : endIndex, 0, { id: `step-${crypto.randomUUID().slice(0, 8)}`, type, config: {} })
  updateDefinition({ steps: next })
}
async function publishVersion() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    if (!workflowId.value || !parsedDefinition.value || !Array.isArray(parsedDefinition.value.steps)) throw new Error('请选择 Workflow 并提供有效的 steps JSON。')
    const version = await call<{ version: number }>(`workflows/${workflowId.value}/versions`, 'POST', { definitionJson: definitionJson.value })
    await call(`workflows/${workflowId.value}/versions/${version.version}/publish`, 'POST', {})
    await Promise.all([loadWorkflows(), chooseWorkflow(workflowId.value)])
    notice.value = `Workflow 第 ${version.version} 版已发布。`
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
async function disableWorkflow() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`workflows/${workflowId.value}/disable`, 'POST', {})
    await loadWorkflows()
    notice.value = 'Workflow 已停用，新任务和未派发任务不能继续执行。'
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
onMounted(async () => {
  try { [cities.value] = await Promise.all([call<Resource[]>('business-resources/cities'), loadWorkflows()]) }
  catch (e) { fail(e) }
})
</script>

<template>
  <div class="page-toolbar"><div><h2>Workflow 管理</h2><p class="muted">选择已有流程编辑版本与风险，发布后才能创建任务。</p></div><button class="action-btn primary" @click="creating = true">新增 Workflow</button></div>
  <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" role="status">{{ notice }}</p>
  <section class="panel form-panel resource-panel">
    <div class="resource-grid">
      <label>已有 Workflow<select :value="workflowId" @change="chooseWorkflow(($event.target as HTMLSelectElement).value)"><option value="">选择 Workflow</option><option v-for="workflow in workflows" :key="workflow.id" :value="workflow.id">{{ workflow.name }} · {{ workflow.status }}</option></select></label>
      <label>历史版本<select :disabled="!workflowId" @change="loadVersion(Number(($event.target as HTMLSelectElement).value))"><option value="">选择版本查看与编辑</option><option v-for="item in versions" :key="item.id" :value="item.version">v{{ item.version }} {{ item.published ? '· 已发布' : '· 草稿' }}</option></select></label>
    </div>
    <template v-if="workflowId">
      <h3>定义与风险</h3>
      <div class="resource-grid"><label>风险级别<select aria-label="Workflow 风险级别" :value="parsedDefinition?.riskLevel || 'Low'" @change="updateDefinition({ riskLevel: ($event.target as HTMLSelectElement).value })"><option v-for="risk in ['Low', 'Medium', 'High', 'Critical']" :key="risk">{{ risk }}</option></select></label>
      <label>审批门禁<select :value="parsedDefinition?.requiresApproval ? 'true' : 'false'" @change="updateDefinition({ requiresApproval: ($event.target as HTMLSelectElement).value === 'true' })"><option value="false">按风险级别自动判定</option><option value="true">必须审批</option></select></label></div>
      <div class="actions"><button v-for="type in stepTypes" :key="type" type="button" class="action-btn" @click="addStep(type)">＋ {{ type }}</button></div>
      <p class="muted">步骤：{{ steps.map(step => `${step.id || '未命名'} (${step.type || '未知'})`).join(' → ') || '无' }}。编辑下方 JSON 可配置 selector、参数、嵌套步骤、timeoutMs 和 retryCount。</p>
      <WorkflowCanvas :steps="steps" />
      <label>Definition JSON<textarea v-model="definitionJson" class="workflow-json" spellcheck="false" /></label>
      <div class="actions"><button type="button" class="action-btn primary" :disabled="busy || !parsedDefinition" @click="publishVersion">创建并发布新版本</button><button v-if="currentWorkflow?.status === 'Published'" type="button" class="action-btn" :disabled="busy" @click="disableWorkflow">停用 Workflow</button></div>
    </template>
  </section>
  <FormDialog :open="creating" title="创建 Workflow" submit-label="创建 Workflow" :busy="busy || !functionId" @close="creating = false" @submit="createWorkflow">
    <label>城市<select :value="cityId" @change="chooseCity(($event.target as HTMLSelectElement).value)"><option value="">选择城市</option><option v-for="city in cities.filter(x => x.enabled)" :key="city.id" :value="city.id">{{ city.name }}</option></select></label>
    <label>系统<select :value="systemId" :disabled="!cityId" @change="chooseSystem(($event.target as HTMLSelectElement).value)"><option value="">选择系统</option><option v-for="system in systems.filter(x => x.enabled)" :key="system.id" :value="system.id">{{ system.name }}</option></select></label>
    <label>功能<select v-model="functionId" :disabled="!systemId"><option value="">选择功能</option><option v-for="fn in functions" :key="fn.id" :value="fn.id">{{ fn.name }}</option></select></label>
    <label>名称<input v-model="workflowName" required maxlength="200" /></label>
    <label>说明<input v-model="workflowDescription" /></label>
  </FormDialog>
</template>

<style scoped>
.workflow-json{width:100%;min-height:300px;resize:vertical;font-family:ui-monospace,monospace;font-size:12px;line-height:1.55}
.actions{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0}
</style>
