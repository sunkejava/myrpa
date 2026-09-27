<script setup lang="ts">
import { ref } from 'vue'
import { workflowRequest } from '../../api/modules/workflows'

const props = defineProps<{ token: string; workflowId: string; definitionJson: string; publishedVersions: number[] }>()
const rows = ref<string>('[]')
const busy = ref(false)
const report = ref('')
const version = ref<number | null>(null)
const taskId = ref('')
function parseRows(): string[] {
  const data: unknown = JSON.parse(rows.value)
  if (!Array.isArray(data) || !data.length || data.length > 100 || data.some(x => !x || typeof x !== 'object' || Array.isArray(x)))
    throw new Error('测试数据必须是 1 至 100 个 JSON 对象组成的数组。')
  return data.map(x => JSON.stringify(x))
}
async function importFile(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  report.value = ''
  if (file.size > 1_000_000) { report.value = '文件不能超过 1 MB。'; return }
  try {
    const content = await file.text()
    if (file.name.toLowerCase().endsWith('.json')) { rows.value = JSON.stringify(JSON.parse(content), null, 2); parseRows(); return }
    if (!file.name.toLowerCase().endsWith('.csv')) throw new Error('仅支持 JSON 或 UTF-8 CSV 文件。')
    const lines: string[][] = []; let field = ''; let record: string[] = []; let quoted = false
    for (let i = 0; i < content.length; i++) {
      const c = content[i]
      if (c === '"' && quoted && content[i + 1] === '"') { field += '"'; i++; continue }
      if (c === '"') { quoted = !quoted; continue }
      if (c === ',' && !quoted) { record.push(field); field = ''; continue }
      if ((c === '\n' || c === '\r') && !quoted) {
        if (c === '\r' && content[i + 1] === '\n') i++
        record.push(field); if (record.some(x => x.trim())) lines.push(record); record = []; field = ''; continue
      }
      field += c
    }
    if (quoted) throw new Error('CSV 引号未闭合。')
    record.push(field); if (record.some(x => x.trim())) lines.push(record)
    if (lines.length < 2 || lines.length > 101) throw new Error('CSV 需要标题行及 1 至 100 条数据。')
    const headers = lines.shift()!.map(x => x.trim().replace(/^\uFEFF/, ''))
    if (headers.some(x => !x) || new Set(headers).size !== headers.length || lines.some(row => row.length !== headers.length)) throw new Error('CSV 标题不能为空或重复，且各行列数必须一致。')
    const schema = (JSON.parse(props.definitionJson) as { parameters?: Record<string, { type?: string }> }).parameters || {}
    const convert = (key: string, value: string): unknown => {
      if (value === '') return null
      if (schema[key]?.type === 'integer' && /^-?\d+$/.test(value)) return Number(value)
      if (schema[key]?.type === 'number' && Number.isFinite(Number(value))) return Number(value)
      if (schema[key]?.type === 'boolean' && /^(true|false)$/i.test(value)) return value.toLowerCase() === 'true'
      return value
    }
    rows.value = JSON.stringify(lines.map(row => Object.fromEntries(headers.map((header, index) => [header, convert(header, row[index])]))), null, 2)
  } catch (e) { report.value = e instanceof Error ? e.message : '导入失败' }
  finally { (event.target as HTMLInputElement).value = '' }
}
async function validate() {
  busy.value = true; report.value = ''; taskId.value = ''
  try {
    const result = await workflowRequest<{ valid: boolean; errors: string[]; count: number }>(props.token, `workflows/${props.workflowId}/test-data/validate`, 'POST', { definitionJson: props.definitionJson, items: parseRows() })
    report.value = result.valid ? `草稿和 ${result.count} 条测试数据静态校验通过；尚未运行浏览器。` : result.errors.join('\n')
    return result.valid
  } catch (e) { report.value = e instanceof Error ? e.message : '校验失败'; return false }
  finally { busy.value = false }
}
async function run() {
  if (!version.value) { report.value = '请选择已发布的版本执行测试。'; return }
  busy.value = true
  try {
    const published = await workflowRequest<{ definitionJson: string; published: boolean }>(props.token, `workflows/${props.workflowId}/versions/${version.value}`)
    if (!published.published) throw new Error('选定的版本尚未发布。')
    const checked = await workflowRequest<{ valid: boolean; errors: string[] }>(props.token, `workflows/${props.workflowId}/test-data/validate`, 'POST', { definitionJson: published.definitionJson, items: parseRows() })
    if (!checked.valid) { report.value = checked.errors.join('\n'); return }
    const created = await workflowRequest<{ id: string; approvalRequired: boolean }>(props.token, 'tasks', 'POST', {
      workflowId: props.workflowId, workflowVersion: version.value, name: `工作流测试 v${version.value}`, items: parseRows(), maxRetries: 0
    })
    taskId.value = created.id
    if (created.approvalRequired) { report.value = '测试任务已创建，需管理员审批后运行；在任务中心查看进度。'; return }
    await workflowRequest(props.token, `tasks/${created.id}/queue`, 'POST')
    report.value = '测试任务已入队；可在任务中心查看各条结果与执行日志。'
  } catch (e) { report.value = e instanceof Error ? e.message : '运行测试失败' }
  finally { busy.value = false }
}
</script>

<template>
  <section class="workflow-test-panel">
    <h3>工作流测试数据</h3>
    <p class="muted">发布前校验当前草稿与测试数据；发布后选定版本，通过正常审批及权限检查运行真实任务。测试任务可能操作业务系统，请使用测试环境与测试账号。</p>
    <label>导入 JSON 数组或 UTF-8 CSV（首行为参数名）<input type="file" accept=".json,.csv,application/json,text/csv" @change="importFile" /></label>
    <label>测试数据（JSON 数组）<textarea v-model="rows" rows="7" spellcheck="false" placeholder='[{"personId":"TEST001"}]' /></label>
    <label>实际执行版本<select v-model.number="version"><option :value="null">选择已发布版本</option><option v-for="item in publishedVersions" :key="item" :value="item">v{{ item }}</option></select></label>
    <div class="actions"><button type="button" class="action-btn" :disabled="busy" @click="validate">校验草稿数据</button><button type="button" class="action-btn primary" :disabled="busy || !publishedVersions.length" @click="run">创建并运行测试任务</button></div>
    <p v-if="report" role="status" class="test-report">{{ report }}</p><p v-if="taskId">任务 ID：{{ taskId }}</p>
  </section>
</template>
<style scoped>.workflow-test-panel{padding:16px;border:1px solid var(--border-color,#d4dce7);border-radius:10px;margin:20px 0}.workflow-test-panel textarea{display:block;width:100%;font-family:ui-monospace,monospace}.workflow-test-panel label{display:block;margin:10px 0}.actions{display:flex;gap:10px;margin-top:10px}.test-report{white-space:pre-wrap}</style>
