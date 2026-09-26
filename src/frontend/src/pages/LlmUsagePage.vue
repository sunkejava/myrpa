<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import SearchForm from '../components/form/SearchForm.vue'
import DataTable from '../components/table/DataTable.vue'
import { useLocale } from '../locales'
import { listUsage, usageSummary, type UsageEntry as Usage, type UsageSummary as Summary } from '../api/modules/usage'

const props = defineProps<{ token: string }>()
const summary = ref<Summary | null>(null)
const rows = ref<Usage[]>([])
const nextAfterId = ref<string | null>(null)
const taskId = ref('')
const filters = ref({ taskId: '' })
const error = ref('')
const { t } = useLocale()
const columns = computed(() => [{ key: 'occurredAt', label: t('usage.time'), sortable: true, format: (value: unknown) => new Date(String(value)).toLocaleString('zh-CN') },
  { key: 'taskId', label: t('usage.taskId') }, { key: 'model', label: t('usage.model'), filterable: true },
  { key: 'providerId', label: t('usage.provider') }, { key: 'inputTokens', label: t('usage.input'), sortable: true },
  { key: 'outputTokens', label: t('usage.output'), sortable: true }, { key: 'totalTokens', label: t('usage.total'), sortable: true }])
function search(values: Record<string, string> = filters.value) { taskId.value = values.taskId || ''; load() }
async function load(afterId?: string) {
  error.value = ''
  try {
    const [result, totals] = await Promise.all([listUsage(props.token, taskId.value, afterId), usageSummary(props.token, taskId.value)])
    rows.value = afterId ? [...rows.value, ...result.items] : result.items
    nextAfterId.value = result.nextAfterId
    summary.value = totals
  } catch (e) { error.value = e instanceof Error ? e.message : t('usage.failure') }
}
onMounted(() => load())
</script>

<template>
  <section class="panel form-panel" style="max-width:none">
    <h2>{{ t('usage.title') }}</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <SearchForm v-model="filters" :fields="[{ key: 'taskId', label: t('usage.taskId'), placeholder: t('usage.taskFilter') }]" @search="search" />
    <p v-if="summary" class="muted">{{ t('usage.calls') }} {{ summary.calls }} {{ t('usage.times') }} · {{ t('usage.input') }} {{ summary.inputTokens }} Token · {{ t('usage.output') }} {{ summary.outputTokens }} Token · {{ t('usage.total') }} {{ summary.totalTokens }} Token</p>
    <DataTable :rows="rows" :columns="columns" :empty-label="t('usage.empty')" filename="llm-usage.csv" @refresh="search" />
    <button v-if="nextAfterId" class="action-btn" @click="load(nextAfterId)">{{ t('usage.more') }}</button>
  </section>
</template>
