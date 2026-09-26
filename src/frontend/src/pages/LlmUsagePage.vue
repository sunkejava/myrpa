<script setup lang="ts">
import { onMounted, ref } from 'vue'
import SearchForm from '../components/form/SearchForm.vue'
import DataTable from '../components/table/DataTable.vue'

type Summary = { calls: number; inputTokens: number; outputTokens: number; totalTokens: number }
type Usage = { id: string; taskId: string | null; model: string; providerId: string; inputTokens: number; outputTokens: number; totalTokens: number; occurredAt: string }
const props = defineProps<{ token: string }>()
const summary = ref<Summary | null>(null)
const rows = ref<Usage[]>([])
const nextAfterId = ref<string | null>(null)
const taskId = ref('')
const filters = ref({ taskId: '' })
const error = ref('')
const columns = [{ key: 'occurredAt', label: '时间', sortable: true, format: (value: unknown) => new Date(String(value)).toLocaleString('zh-CN') },
  { key: 'taskId', label: '任务 ID' }, { key: 'model', label: '模型', filterable: true },
  { key: 'providerId', label: 'Provider' }, { key: 'inputTokens', label: '输入', sortable: true },
  { key: 'outputTokens', label: '输出', sortable: true }, { key: 'totalTokens', label: '合计', sortable: true }]
function search(values: Record<string, string> = filters.value) { taskId.value = values.taskId || ''; load() }
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
  <section class="panel form-panel" style="max-width:none">
    <h2>我的 LLM 用量</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <SearchForm v-model="filters" :fields="[{ key: 'taskId', label: '任务 ID（可选）', placeholder: '按任务筛选' }]" @search="search" />
    <p v-if="summary" class="muted">调用 {{ summary.calls }} 次 · 输入 {{ summary.inputTokens }} Token · 输出 {{ summary.outputTokens }} Token · 总计 {{ summary.totalTokens }} Token</p>
    <DataTable :rows="rows" :columns="columns" empty-label="暂无 Token 用量。" filename="llm-usage.csv" @refresh="search" />
    <button v-if="nextAfterId" class="action-btn" @click="load(nextAfterId)">加载更多</button>
  </section>
</template>
