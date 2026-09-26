<script setup lang="ts">
import { ref } from 'vue'

type Plan = { cityId: string; systemId: string; functionId: string; action: string; riskLevel: string; workflowId: string; workflowVersion: number; requiresConfirmation: boolean }
type PlanResponse = { success: boolean; summary: string; ambiguities: string[]; plan: Plan | null }
const props = defineProps<{ token: string }>()
const emit = defineEmits<{ submitted: [] }>()
const instruction = ref('')
const plan = ref<PlanResponse | null>(null)
const error = ref('')
const busy = ref(false)

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
  catch (e) { error.value = e instanceof Error ? e.message : '任务规划失败' }
  finally { busy.value = false }
}
async function execute() {
  if (!plan.value?.plan) return
  busy.value = true; error.value = ''
  try {
    await call('/api/agent/execute', { instruction: instruction.value, confirmed: true })
    plan.value = null; instruction.value = ''; emit('submitted')
  } catch (e) { error.value = e instanceof Error ? e.message : '任务提交失败' }
  finally { busy.value = false }
}
</script>

<template>
  <p v-if="error" class="error" role="alert">{{ error }}</p>
  <section class="panel form-panel">
    <span class="eyebrow">AGENT PLANNER</span>
    <h2>今天需要帮你处理什么？</h2>
    <p class="muted">请明确城市、业务系统和业务功能。提交前可查看规划结果及风险。</p>
    <form @submit.prevent="makePlan">
      <label>任务描述<textarea v-model.trim="instruction" required rows="5" placeholder="例如：查询青岛社保人员状态"></textarea></label>
      <button class="action-btn primary" :disabled="busy">{{ busy ? '正在处理…' : '生成计划' }}</button>
    </form>
    <div v-if="plan" class="plan-result">
      <h3>规划结果</h3><p>{{ plan.summary }}</p>
      <ul v-if="plan.ambiguities.length"><li v-for="reason in plan.ambiguities" :key="reason">{{ reason }}</li></ul>
      <dl v-if="plan.plan"><dt>城市 ID</dt><dd>{{ plan.plan.cityId }}</dd><dt>系统 ID</dt><dd>{{ plan.plan.systemId }}</dd><dt>功能 ID</dt><dd>{{ plan.plan.functionId }}</dd><dt>动作 / 风险</dt><dd>{{ plan.plan.action }} / {{ plan.plan.riskLevel }}</dd><dt>Workflow</dt><dd>{{ plan.plan.workflowId }} v{{ plan.plan.workflowVersion }}</dd></dl>
      <button v-if="plan.plan" class="action-btn primary" :disabled="busy" @click="execute">{{ plan.plan.requiresConfirmation ? '确认并提交任务' : '提交任务' }}</button>
    </div>
  </section>
</template>
