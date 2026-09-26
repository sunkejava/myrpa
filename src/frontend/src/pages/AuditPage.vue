<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import SearchForm from '../components/form/SearchForm.vue'
import DataTable from '../components/table/DataTable.vue'
import { apiRequest } from '../api/http'
import { useLocale } from '../locales'

type Entry = { id: string; createdAt: string; actor: string; action: string; resource: string; resourceId: string; result: string; summary: string }
const props = defineProps<{ token: string }>()
const rows = ref<Entry[]>([])
const filters = ref({ actor: '', resource: '' })
const error = ref('')
const busy = ref(false)
const { t } = useLocale()
const fields = computed(() => [{ key: 'actor', label: t('audit.actor'), placeholder: t('audit.exact') }, { key: 'resource', label: t('audit.resource'), placeholder: t('audit.exact') }])
const columns = computed(() => [{ key: 'createdAt', label: t('audit.time'), sortable: true, format: (value: unknown) => new Date(String(value)).toLocaleString('zh-CN') },
  { key: 'actor', label: t('audit.actor'), sortable: true, filterable: true }, { key: 'action', label: t('audit.action'), sortable: true },
  { key: 'resource', label: t('audit.resource') }, { key: 'result', label: t('audit.result') }, { key: 'summary', label: t('audit.summary') }])
async function load(values: Record<string, string> = filters.value) {
  error.value = ''; busy.value = true
  try {
    const query = new URLSearchParams({ limit: '100' })
    if (values.actor?.trim()) query.set('actor', values.actor.trim())
    if (values.resource?.trim()) query.set('resource', values.resource.trim())
    rows.value = await apiRequest<Entry[]>(`audit?${query}`, props.token)
  } catch (e) { error.value = e instanceof Error ? e.message : t('audit.failure') }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel" style="max-width:none">
    <h2>{{ t('audit.title') }}</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <SearchForm v-model="filters" :fields="fields" :loading="busy" @search="load" />
    <DataTable :rows="rows" :columns="columns" :loading="busy" :empty-label="t('audit.empty')" filename="audit.csv" @refresh="load" />
  </section>
</template>
