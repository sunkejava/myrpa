<script setup lang="ts">
import { computed, ref, watch } from 'vue'

type Step = { id?: string; type?: string; config?: Record<string, unknown>; timeoutMs?: number; retryCount?: number; requiredAction?: string }
const props = defineProps<{ step: Step; index: number; total: number }>()
const emit = defineEmits<{ update: [index: number, step: Step]; remove: [index: number]; move: [from: number, to: number] }>()
const configText = ref('{}')
const error = ref('')
const fields: Record<string, { key: string; label: string; hint: string; numeric?: boolean }[]> = {
  Navigate: [{ key: 'url', label: '页面地址', hint: '{{systemBaseUrl}}' }],
  Click: [{ key: 'selector', label: '点击目标', hint: '[data-testid=submit]' }],
  Input: [{ key: 'selector', label: '输入框', hint: '[data-testid=person-id]' }, { key: 'value', label: '填入内容', hint: '{{personId}}' }],
  Select: [{ key: 'selector', label: '下拉框', hint: 'select[name=period]' }, { key: 'value', label: '选项值', hint: '2026-09' }],
  Wait: [{ key: 'milliseconds', label: '等待毫秒数', hint: '500', numeric: true }],
  WaitForElement: [{ key: 'selector', label: '等待元素', hint: '[data-testid=result]' }],
  Extract: [{ key: 'selector', label: '读取元素', hint: '[data-testid=result]' }, { key: 'output', label: '结果字段名', hint: 'personName' }],
  Assert: [{ key: 'selector', label: '检查元素', hint: '[data-testid=success]' }, { key: 'contains', label: '应包含的文字', hint: '操作成功' }],
  Upload: [{ key: 'selector', label: '上传输入框', hint: 'input[type=file]' }, { key: 'path', label: '节点上的文件路径', hint: 'artifacts/input.xlsx' }],
  Download: [{ key: 'selector', label: '下载按钮', hint: '[data-testid=export]' }, { key: 'path', label: '保存到节点的路径', hint: 'artifacts/result.xlsx' }],
  Screenshot: [{ key: 'path', label: '截图路径', hint: 'artifacts/screen.png' }],
  Condition: [{ key: 'selector', label: '判断元素', hint: '[data-testid=status]' }, { key: 'contains', label: '分支匹配文字', hint: '通过' }],
  Loop: [{ key: 'count', label: '重复次数', hint: '1', numeric: true }],
  HumanTask: [{ key: 'interventionType', label: '介入类型', hint: 'Captcha / UKeyConfirmation / ManualApproval' }, { key: 'title', label: '提示标题', hint: '请插入 UKey 并完成授权' }]
}
const simpleFields = computed(() => fields[props.step.type || ''] || [])
watch(() => props.step, step => { configText.value = JSON.stringify(step.config || {}, null, 2); error.value = '' }, { immediate: true })
function update(patch: Partial<Step>) { emit('update', props.index, { ...props.step, ...patch }) }
function updateField(key: string, value: string, numeric?: boolean) {
  update({ config: { ...(props.step.config || {}), [key]: numeric ? Number(value) : value } })
}
function saveConfig() {
  try {
    const value: unknown = JSON.parse(configText.value)
    if (!value || typeof value !== 'object' || Array.isArray(value)) throw new Error('配置必须是 JSON 对象')
    update({ config: value as Record<string, unknown> }); error.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '配置 JSON 格式错误' }
}
const safeRetry = ['navigate', 'waitforelement', 'assert', 'extract']
</script>

<template>
  <details class="workflow-step-editor">
    <summary>{{ index + 1 }} · {{ step.id || '未命名步骤' }} · {{ step.type || '未知类型' }}</summary>
    <div class="workflow-step-fields">
      <label>步骤 ID<input :value="step.id || ''" maxlength="128" @change="update({ id: ($event.target as HTMLInputElement).value.trim() })" /></label>
      <label>超时（毫秒）<input type="number" min="100" max="120000" :value="step.timeoutMs || 30000" @change="update({ timeoutMs: Number(($event.target as HTMLInputElement).value) })" /></label>
      <label v-if="safeRetry.includes((step.type || '').toLowerCase())">失败重试次数<input type="number" min="0" max="3" :value="step.retryCount || 0" @change="update({ retryCount: Number(($event.target as HTMLInputElement).value) })" /></label>
      <label>步骤业务权限动作<input :value="step.requiredAction || ''" placeholder="不需要时留空" @change="update({ requiredAction: ($event.target as HTMLInputElement).value.trim() || undefined })" /></label>
    </div>
    <div v-if="simpleFields.length" class="workflow-step-fields">
      <label v-for="field in simpleFields" :key="field.key">{{ field.label }}
        <input :type="field.numeric ? 'number' : 'text'" :min="field.numeric ? 0 : undefined" :value="step.config?.[field.key] ?? ''" :placeholder="field.hint" @change="updateField(field.key, ($event.target as HTMLInputElement).value, field.numeric)" />
      </label>
    </div>
    <p v-if="step.type === 'Extract'" class="muted">提取结果在任务详情中显示，后续步骤可使用字段名变量引用。</p>
    <p v-if="step.type === 'HumanTask'" class="muted">支持 Captcha、UKeyConfirmation、FaceAuthentication、ManualApproval。任务所有人确认后在原浏览器会话继续。</p>
    <label>节点配置 JSON（selector、url、value、path、steps 等）<textarea v-model="configText" rows="6" spellcheck="false" @blur="saveConfig" /></label>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <div class="actions">
      <button class="action-btn" type="button" @click="saveConfig">保存节点配置</button>
      <button class="action-btn" type="button" :disabled="index === 0" @click="emit('move', index, index - 1)">上移</button>
      <button class="action-btn" type="button" :disabled="index === total - 1" @click="emit('move', index, index + 1)">下移</button>
      <button class="action-btn" type="button" @click="emit('remove', index)">删除节点</button>
    </div>
  </details>
</template>

<style scoped>
.workflow-step-editor{border:1px solid var(--border-color,#d4dce7);border-radius:10px;padding:12px;margin:8px 0}
.workflow-step-editor summary{cursor:pointer;font-weight:600}
.workflow-step-fields{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:10px;margin:12px 0}
.workflow-step-editor textarea{width:100%;font-family:ui-monospace,monospace}
.actions{display:flex;gap:8px;flex-wrap:wrap;margin-top:12px}
</style>
