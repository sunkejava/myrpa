<script setup lang="ts">
import { ref, watch } from 'vue'

type Step = { id?: string; type?: string; config?: Record<string, unknown>; timeoutMs?: number; retryCount?: number; requiredAction?: string }
const props = defineProps<{ step: Step; index: number; total: number }>()
const emit = defineEmits<{ update: [index: number, step: Step]; remove: [index: number]; move: [from: number, to: number] }>()
const configText = ref('{}')
const error = ref('')
watch(() => props.step, step => { configText.value = JSON.stringify(step.config || {}, null, 2); error.value = '' }, { immediate: true })
function update(patch: Partial<Step>) { emit('update', props.index, { ...props.step, ...patch }) }
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
