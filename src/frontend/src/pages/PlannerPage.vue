<script setup lang="ts">
import { ref } from 'vue'
import { useLocale } from '../locales'

type Plan = { cityId: string; systemId: string; functionId: string; action: string; riskLevel: string; workflowId: string; workflowVersion: number; requiresConfirmation: boolean }
type PlanResponse = { success: boolean; summary: string; ambiguities: string[]; plan: Plan | null }
const props = defineProps<{ token: string }>()
const emit = defineEmits<{ submitted: [] }>()
const instruction = ref('')
const plan = ref<PlanResponse | null>(null)
const error = ref('')
const busy = ref(false)
const { t } = useLocale()

async function call<T>(path: string, body: object): Promise<T> {
  const response = await fetch(path, { method: 'POST', headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' }, body: JSON.stringify(body) })
  if (!response.ok) {
    const details: unknown = await response.json().catch(() => null)
    throw new Error(details && typeof details === 'object' && 'message' in details ? String(details.message) : `请求失败 (${response.status})`)
  }
  return await response.json() as T
}
async function makePlan() {
  busy.value = true; error.value = ''; plan.value = null
  try { plan.value = await call<PlanResponse>('/api/agent/plan', { instruction: instruction.value }) }
  catch (e) { error.value = e instanceof Error ? e.message : t('task.planFailed') }
  finally { busy.value = false }
}
async function execute() {
  if (!plan.value?.plan) return
  busy.value = true; error.value = ''
  try {
    await call('/api/agent/execute', { instruction: instruction.value, confirmed: true })
    plan.value = null; instruction.value = ''; emit('submitted')
  } catch (e) { error.value = e instanceof Error ? e.message : t('task.submitFailed') }
  finally { busy.value = false }
}
</script>

<template>
  <p v-if="error" class="error" role="alert">{{ error }}</p>
  <section class="panel form-panel">
    <span class="eyebrow">AGENT PLANNER</span>
    <h2>{{ t('task.plannerTitle') }}</h2>
    <p class="muted">{{ t('task.plannerHelp') }}</p>
    <form @submit.prevent="makePlan">
      <label>{{ t('task.description') }}<textarea v-model.trim="instruction" required rows="5" :placeholder="t('task.plannerExample')"></textarea></label>
      <button class="action-btn primary" :disabled="busy">{{ t(busy ? 'task.planning' : 'task.plan') }}</button>
    </form>
    <div v-if="plan" class="plan-result">
      <h3>{{ t('task.planResult') }}</h3><p>{{ plan.summary }}</p>
      <ul v-if="plan.ambiguities.length"><li v-for="reason in plan.ambiguities" :key="reason">{{ reason }}</li></ul>
      <dl v-if="plan.plan"><dt>{{ t('task.city') }}</dt><dd>{{ plan.plan.cityId }}</dd><dt>{{ t('task.system') }}</dt><dd>{{ plan.plan.systemId }}</dd><dt>{{ t('task.function') }}</dt><dd>{{ plan.plan.functionId }}</dd><dt>{{ t('task.risk') }}</dt><dd>{{ plan.plan.action }} / {{ plan.plan.riskLevel }}</dd><dt>Workflow</dt><dd>{{ plan.plan.workflowId }} v{{ plan.plan.workflowVersion }}</dd></dl>
      <button v-if="plan.plan" class="action-btn primary" :disabled="busy" @click="execute">{{ t(plan.plan.requiresConfirmation ? 'task.submitConfirm' : 'task.submit') }}</button>
    </div>
  </section>
</template>
