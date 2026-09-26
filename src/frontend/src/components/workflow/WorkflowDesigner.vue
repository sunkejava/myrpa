<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import WorkflowCanvas from './WorkflowCanvas.vue'
import WorkflowTemplateGallery from './WorkflowTemplateGallery.vue'
import WorkflowStepEditor from './WorkflowStepEditor.vue'
import type { WorkflowTemplate } from '../../data/workflow-templates/catalog'
import FormDialog from '../common/FormDialog.vue'
import { workflowRequest, type WorkflowResource as Resource, type WorkflowItem as Workflow, type WorkflowVersion as Version } from '../../api/modules/workflows'
import { useLocale } from '../../locales'

const props = defineProps<{ token: string }>()
const { t } = useLocale()
const cities = ref<Resource[]>([])
const systems = ref<Resource[]>([])
const functions = ref<Resource[]>([])
const workflows = ref<Workflow[]>([])
const versions = ref<Version[]>([])
const cityId = ref('')
const systemId = ref('')
const functionId = ref('')
const workflowId = ref('')
const selectedVersion = ref<number | null>(null)
const workflowName = ref('')
const workflowDescription = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)
const creating = ref(false)
const pendingTemplate = ref<WorkflowTemplate | null>(null)
type Step = { id?: string; type?: string; config?: Record<string, unknown>; timeoutMs?: number; retryCount?: number; requiredAction?: string }
const defaultDefinition = () => JSON.stringify({ version: 1, riskLevel: 'Low', requiresApproval: false, steps: [{ id: 'step-1', type: 'End' }] }, null, 2)
const definitionJson = ref(defaultDefinition())
const stepTypes = ['Navigate', 'Click', 'Input', 'Select', 'Wait', 'WaitForElement', 'Extract', 'Upload', 'Download', 'Screenshot', 'Condition', 'Loop', 'SubWorkflow', 'HumanTask', 'Assert', 'End']
const currentWorkflow = computed(() => workflows.value.find(x => x.id === workflowId.value))
const parsedDefinition = computed(() => {
  try {
    const value: unknown = JSON.parse(definitionJson.value)
    return value && typeof value === 'object' && !Array.isArray(value) ? value as Record<string, unknown> : null
  } catch { return null }
})
const steps = computed(() => Array.isArray(parsedDefinition.value?.steps) ? parsedDefinition.value.steps as Step[] : [])

const call = <T,>(path: string, method = 'GET', body?: object) => workflowRequest<T>(props.token, path, method, body)
function fail(e: unknown) { error.value = e instanceof Error ? e.message : t('workflow.operationFailed') }
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
  workflowId.value = id; versions.value = []; selectedVersion.value = null; definitionJson.value = defaultDefinition(); notice.value = ''
  try {
    const fetched = id ? await call<Version[]>(`workflows/${id}/versions`) : []
    if (workflowId.value !== id) return
    versions.value = fetched
    if (id && fetched.length) await loadVersion(fetched[0].version)
  }
  catch (e) { fail(e) }
}
async function loadVersion(version: number) {
  if (!version || !workflowId.value) return
  const requestedWorkflowId = workflowId.value
  try {
    const result = await call<{ definitionJson: string }>(`workflows/${requestedWorkflowId}/versions/${version}`)
    if (workflowId.value !== requestedWorkflowId) return
    definitionJson.value = JSON.stringify(JSON.parse(result.definitionJson), null, 2)
    selectedVersion.value = version
    notice.value = t('workflow.versionLoaded').replace('{version}', String(version))
  } catch (e) { fail(e) }
}
async function createWorkflow() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    const result = await call<{ id: string }>('workflows', 'POST', {
      businessFunctionId: functionId.value, name: workflowName.value.trim(), description: workflowDescription.value.trim()
    })
    await loadWorkflows(); await chooseWorkflow(result.id)
    if (pendingTemplate.value) {
      definitionJson.value = JSON.stringify(pendingTemplate.value.definition, null, 2)
      notice.value = `已载入「${pendingTemplate.value.name}」草稿，请核对资源与步骤后再发布。`
      pendingTemplate.value = null
    }
    creating.value = false
    if (!notice.value) notice.value = t('workflow.created')
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
function updateDefinition(patch: Record<string, unknown>) {
  if (!parsedDefinition.value) { error.value = t('workflow.invalidJson'); return }
  definitionJson.value = JSON.stringify({ ...parsedDefinition.value, ...patch }, null, 2)
}
function selectAdapter(raw: string) {
  const code = raw.trim()
  if (!parsedDefinition.value) return
  const prior = typeof parsedDefinition.value.adapter === 'string' ? parsedDefinition.value.adapter : 'direct'
  const current = parsedDefinition.value.executionRequirement
  const requirement: Record<string, unknown> = current && typeof current === 'object' && !Array.isArray(current)
    ? { ...current as Record<string, unknown> } : {}
  const oldCapabilities = Array.isArray(requirement.requiredCapabilities) ? requirement.requiredCapabilities : []
  requirement.requiredCapabilities = oldCapabilities.filter(capability => capability !== `Adapter:${prior}`)
  if (code && code !== 'direct') requirement.requiredCapabilities = [...requirement.requiredCapabilities as unknown[], `Adapter:${code}`]
  updateDefinition({ adapter: code || 'direct', executionRequirement: requirement })
}
function addStep(type: string) {
  if (!parsedDefinition.value) { error.value = t('workflow.invalidJson'); return }
  const next = [...steps.value]
  const endIndex = next.findIndex(step => step.type?.toLowerCase() === 'end')
  const config = type === 'Loop' || type === 'SubWorkflow' ? { steps: [] } : type === 'Condition' ? { then: [], else: [] } : {}
  next.splice(endIndex < 0 ? next.length : endIndex, 0, { id: `step-${crypto.randomUUID().slice(0, 8)}`, type, config })
  updateDefinition({ steps: next })
}
function editStep(index: number, step: Step) { const next = [...steps.value]; next[index] = step; updateDefinition({ steps: next }) }
function removeStep(index: number) { updateDefinition({ steps: steps.value.filter((_, i) => i !== index) }) }
function moveStep(from: number, to: number) {
  const next = [...steps.value]; next.splice(to, 0, ...next.splice(from, 1)); updateDefinition({ steps: next })
}
function selectTemplate(template: WorkflowTemplate) {
  if (workflowId.value) {
    definitionJson.value = JSON.stringify(template.definition, null, 2)
    notice.value = `已载入「${template.name}」草稿，发布前请核对当前 Workflow 绑定的业务功能。`
  } else {
    pendingTemplate.value = template
    workflowName.value = template.name
    workflowDescription.value = template.summary
    creating.value = true
    notice.value = `请选择「${template.resource}」对应的城市、系统和业务功能。`
  }
}
async function publishVersion() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    if (!workflowId.value || !parsedDefinition.value || !Array.isArray(parsedDefinition.value.steps)) throw new Error(t('workflow.invalidSteps'))
    if (definitionJson.value.includes('replace-')) throw new Error('示例中的 replace-* 页面选择器尚未替换，不能发布。')
    const version = await call<{ version: number }>(`workflows/${workflowId.value}/versions`, 'POST', { definitionJson: definitionJson.value })
    await call(`workflows/${workflowId.value}/versions/${version.version}/publish`, 'POST', {})
    await Promise.all([loadWorkflows(), chooseWorkflow(workflowId.value)])
    notice.value = t('workflow.versionPublished').replace('{version}', String(version.version))
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
async function disableWorkflow() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`workflows/${workflowId.value}/disable`, 'POST', {})
    await loadWorkflows()
    notice.value = t('workflow.disabled')
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
async function enableWorkflow() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`workflows/${workflowId.value}/publish`, 'POST')
    await loadWorkflows()
    notice.value = t('workflow.reenabled')
  } catch (e) { fail(e) }
  finally { busy.value = false }
}
onMounted(async () => {
  try { [cities.value] = await Promise.all([call<Resource[]>('business-resources/cities'), loadWorkflows()]) }
  catch (e) { fail(e) }
})
</script>

<template>
  <div class="page-toolbar"><div><h2>{{ t('workflow.title') }}</h2><p class="muted">{{ t('workflow.help') }}</p></div><button class="action-btn primary" @click="creating = true">{{ t('workflow.add') }}</button></div>
  <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" role="status">{{ notice }}</p>
  <WorkflowTemplateGallery @select="selectTemplate" />
  <section class="panel form-panel resource-panel">
    <div class="resource-grid">
      <label>{{ t('workflow.existing') }}<select :value="workflowId" @change="chooseWorkflow(($event.target as HTMLSelectElement).value)"><option value="">{{ t('workflow.chooseWorkflow') }}</option><option v-for="workflow in workflows" :key="workflow.id" :value="workflow.id">{{ workflow.name }} · {{ workflow.status }}</option></select></label>
      <label>{{ t('workflow.history') }}<select :value="selectedVersion ?? ''" :disabled="!workflowId" @change="loadVersion(Number(($event.target as HTMLSelectElement).value))"><option value="">{{ t('workflow.chooseVersion') }}</option><option v-for="item in versions" :key="item.id" :value="item.version">v{{ item.version }} · {{ t(item.published ? 'workflow.published' : 'workflow.draft') }}</option></select></label>
    </div>
    <template v-if="workflowId">
      <h3>{{ t('workflow.definition') }}</h3>
      <div class="resource-grid"><label>{{ t('workflow.risk') }}<select :aria-label="t('workflow.riskAria')" :value="parsedDefinition?.riskLevel || 'Low'" @change="updateDefinition({ riskLevel: ($event.target as HTMLSelectElement).value })"><option v-for="risk in ['Low', 'Medium', 'High', 'Critical']" :key="risk">{{ risk }}</option></select></label>
      <label>{{ t('workflow.approval') }}<select :value="parsedDefinition?.requiresApproval ? 'true' : 'false'" @change="updateDefinition({ requiresApproval: ($event.target as HTMLSelectElement).value === 'true' })"><option value="false">{{ t('workflow.autoApproval') }}</option><option value="true">{{ t('workflow.mustApprove') }}</option></select></label>
      <label>站点适配器编码<input :value="parsedDefinition?.adapter || 'direct'" maxlength="64" placeholder="direct 或已配置的 Adapter 编码" @change="selectAdapter(($event.target as HTMLInputElement).value)" /></label></div>
      <details class="workflow-tools"><summary class="action-btn">{{ t('workflow.addStep') }}</summary><div class="actions"><button v-for="type in stepTypes" :key="type" type="button" class="action-btn" @click="addStep(type)">＋ {{ type }}</button></div></details>
      <p class="muted">{{ t('workflow.steps') }}：{{ steps.map(step => `${step.id || t('workflow.unnamed')} (${step.type || t('workflow.unknown')})`).join(' → ') || t('workflow.none') }}。{{ t('workflow.jsonHelp') }}</p>
      <WorkflowCanvas :steps="steps" />
      <section class="workflow-step-list"><h3>逐节点配置</h3><WorkflowStepEditor v-for="(step, index) in steps" :key="`${step.id || step.type}-${index}`" :step="step" :index="index" :total="steps.length" @update="editStep" @remove="removeStep" @move="moveStep" /></section>
      <details class="workflow-tools"><summary class="action-btn">{{ t('workflow.jsonEditor') }}</summary><label>{{ t('workflow.definitionJson') }}<textarea v-model="definitionJson" class="workflow-json" spellcheck="false" /></label></details>
      <div class="actions"><button type="button" class="action-btn primary" :disabled="busy || !parsedDefinition" @click="publishVersion">{{ t('workflow.publish') }}</button><button v-if="currentWorkflow?.status === 'Published'" type="button" class="action-btn" :disabled="busy" @click="disableWorkflow">{{ t('workflow.disable') }}</button><button v-if="currentWorkflow?.status === 'Disabled' && versions.some(version => version.published)" type="button" class="action-btn" :disabled="busy" @click="enableWorkflow">{{ t('workflow.reenable') }}</button></div>
    </template>
  </section>
  <FormDialog :open="creating" :title="t('workflow.create')" :submit-label="t('workflow.create')" :busy="busy || !functionId" @close="creating = false" @submit="createWorkflow">
    <label>{{ t('workflow.city') }}<select :value="cityId" @change="chooseCity(($event.target as HTMLSelectElement).value)"><option value="">{{ t('workflow.chooseCity') }}</option><option v-for="city in cities.filter(x => x.enabled)" :key="city.id" :value="city.id">{{ city.name }}</option></select></label>
    <label>{{ t('workflow.system') }}<select :value="systemId" :disabled="!cityId" @change="chooseSystem(($event.target as HTMLSelectElement).value)"><option value="">{{ t('workflow.chooseSystem') }}</option><option v-for="system in systems.filter(x => x.enabled)" :key="system.id" :value="system.id">{{ system.name }}</option></select></label>
    <label>{{ t('workflow.function') }}<select v-model="functionId" :disabled="!systemId"><option value="">{{ t('workflow.chooseFunction') }}</option><option v-for="fn in functions" :key="fn.id" :value="fn.id">{{ fn.name }}</option></select></label>
    <label>{{ t('workflow.name') }}<input v-model="workflowName" required maxlength="200" /></label>
    <label>{{ t('workflow.description') }}<input v-model="workflowDescription" /></label>
  </FormDialog>
</template>

<style scoped>
.workflow-json{width:100%;min-height:300px;resize:vertical;font-family:ui-monospace,monospace;font-size:12px;line-height:1.55}
.actions{display:flex;gap:8px;flex-wrap:wrap;margin:12px 0}
</style>
