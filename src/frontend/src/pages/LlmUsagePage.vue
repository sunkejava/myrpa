<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Summary = { calls: number; inputTokens: number; outputTokens: number; totalTokens: number }
type Usage = { id: string; taskId: string | null; model: string; providerId: string; inputTokens: number; outputTokens: number; totalTokens: number; occurredAt: string }
const props = defineProps<{ token: string }>()
const summary = ref<Summary | null>(null)
const rows = ref<Usage[]>([])
const nextAfterId = ref<string | null>(null)
const taskId = ref('')
const error = ref('')
async function load(afterId?: string) {
  error.value = ''
  try {
    const query = new URLSearchParams({ take: '50' })
    if (taskId.value.trim()) query.set('taskId', taskId.value.trim())
    if (afterId) query.set('afterId', afterId)
    const headers = { Authorization: `Bearer ${props.token}` }
    const [itemsResponse, summaryResponse] = await Promise.all([
      fetch(`/api/llm-usage?${query}`, { headers }), fetch(`/api/llm-usage/summary${taskId.value.trim() ? `?taskId=${encodeURIComponent(taskId.value.trim())}` : ''}`, { headers })
    ])
    if (!itemsResponse.ok || !summaryResponse.ok) throw new Error(`用量查询失败 (${itemsResponse.ok ? summaryResponse.status : itemsResponse.status})`)
    const result = await itemsResponse.json() as { items: Usage[]; nextAfterId: string | null }
    rows.value = afterId ? [...rows.value, ...result.items] : result.items
    nextAfterId.value = result.nextAfterId
    summary.value = await summaryResponse.json() as Summary
  } catch (e) { error.value = e instanceof Error ? e.message : '用量查询失败' }
}
onMounted(() => load())
</script>

<template>
  <section class="panel form-panel">
    <h2>我的 LLM 用量</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <form @submit.prevent="load()"><label>任务 ID（可选）<input v-model.trim="taskId" placeholder="按任务筛选" /></label><button class="action-btn primary">查询</button></form>
    <p v-if="summary" class="muted">调用 {{ summary.calls }} 次 · 输入 {{ summary.inputTokens }} Token · 输出 {{ summary.outputTokens }} Token · 总计 {{ summary.totalTokens }} Token</p>
    <div class="table-wrap"><table><thead><tr><th>时间</th><th>任务 ID</th><th>模型 / Provider</th><th>输入</th><th>输出</th><th>合计</th></tr></thead><tbody>
      <tr v-for="row in rows" :key="row.id"><td>{{ new Date(row.occurredAt).toLocaleString('zh-CN') }}</td><td>{{ row.taskId || '—' }}</td><td>{{ row.model }}<small>{{ row.providerId }}</small></td><td>{{ row.inputTokens }}</td><td>{{ row.outputTokens }}</td><td>{{ row.totalTokens }}</td></tr>
    </tbody></table><p v-if="!rows.length" class="muted empty">暂无 Token 用量。</p></div>
    <button v-if="nextAfterId" class="action-btn" @click="load(nextAfterId)">加载更多</button>
  </section>
</template>
