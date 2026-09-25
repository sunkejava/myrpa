<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Checkpoint = { stepId: string; eventType: string }
type Pending = { executionId: string; taskId: string; name: string; subjectId: string; error: string | null; checkpoints: Checkpoint[] }
const props = defineProps<{ token: string }>()
const rows = ref<Pending[]>([])
const evidence = ref<Record<string, string>>({})
const decision = ref<Record<string, 'Submitted' | 'NotSubmitted'>>({})
const busy = ref(false)
const error = ref('')
const notice = ref('')

async function load() {
  error.value = ''
  try {
    const response = await fetch('/api/task-reconciliations', { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`加载核验列表失败 (${response.status})`)
    rows.value = await response.json() as Pending[]
  } catch (e) { error.value = e instanceof Error ? e.message : '加载核验列表失败' }
}

async function confirm(item: Pending) {
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    const response = await fetch(`/api/task-reconciliations/${item.executionId}/decide`, {
      method: 'POST', headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' },
      body: JSON.stringify({ decision: decision.value[item.executionId], evidenceReference: evidence.value[item.executionId]?.trim() })
    })
    if (!response.ok) {
      const details = await response.json().catch(() => null) as { message?: string } | null
      throw new Error(details?.message || `核验失败 (${response.status})`)
    }
    notice.value = `任务 ${item.taskId} 的核验结论已记录。`
    await load()
  } catch (e) { error.value = e instanceof Error ? e.message : '核验失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>高风险执行核验</span><button class="action-btn" @click="load">刷新</button></div>
    <p class="muted">先在外部系统核对业务状态，记录查询单号或截图编号。确认已提交则结案；确认未提交才允许重新执行。任务发起人不能自行核验。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" role="status">{{ notice }}</p>
    <div class="table-wrap"><table><thead><tr><th>任务 / 执行</th><th>失败原因与检查点</th><th>外部凭据</th><th>结论</th><th>操作</th></tr></thead>
      <tbody><tr v-for="item in rows" :key="item.executionId">
        <td>{{ item.name }}<br><small>{{ item.taskId }}</small><br><small>{{ item.executionId }}</small></td>
        <td><p>{{ item.error || '未记录错误' }}</p><small v-for="checkpoint in item.checkpoints" :key="checkpoint.stepId + checkpoint.eventType">{{ checkpoint.stepId }} · {{ checkpoint.eventType }}<br></small></td>
        <td><input v-model.trim="evidence[item.executionId]" :aria-label="`外部核验凭据 ${item.taskId}`" maxlength="256" placeholder="查询单号或截图编号" /></td>
        <td><select v-model="decision[item.executionId]" :aria-label="`核验结论 ${item.taskId}`"><option value="" disabled>选择结论</option><option value="Submitted">已提交，结案</option><option value="NotSubmitted">未提交，重新执行</option></select></td>
        <td><button class="action-btn primary" :disabled="busy || !evidence[item.executionId] || !decision[item.executionId]" @click="confirm(item)">确认核验</button></td>
      </tr></tbody></table><p v-if="!rows.length" class="muted empty">暂无需要核验的高风险失败执行。</p></div>
  </section>
</template>
