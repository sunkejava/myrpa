<script setup lang="ts">
import { onMounted, ref } from 'vue'
import SearchForm from '../components/form/SearchForm.vue'
import DataTable from '../components/table/DataTable.vue'
import { apiRequest } from '../api/http'

type Entry = { id: string; createdAt: string; actor: string; action: string; resource: string; resourceId: string; result: string; summary: string }
const props = defineProps<{ token: string }>()
const rows = ref<Entry[]>([])
const filters = ref({ actor: '', resource: '' })
const error = ref('')
const busy = ref(false)
const fields = [{ key: 'actor', label: '操作者', placeholder: '精确匹配' }, { key: 'resource', label: '资源', placeholder: '精确匹配' }]
const columns = [{ key: 'createdAt', label: '时间', sortable: true, format: (value: unknown) => new Date(String(value)).toLocaleString('zh-CN') },
  { key: 'actor', label: '操作者', sortable: true, filterable: true }, { key: 'action', label: '动作', sortable: true },
  { key: 'resource', label: '资源' }, { key: 'result', label: '结果' }, { key: 'summary', label: '说明' }]
async function load(values: Record<string, string> = filters.value) {
  error.value = ''; busy.value = true
  try {
    const query = new URLSearchParams({ limit: '100' })
    if (values.actor?.trim()) query.set('actor', values.actor.trim())
    if (values.resource?.trim()) query.set('resource', values.resource.trim())
    rows.value = await apiRequest<Entry[]>(`audit?${query}`, props.token)
  } catch (e) { error.value = e instanceof Error ? e.message : '审计查询失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel" style="max-width:none">
    <h2>审计记录</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <SearchForm v-model="filters" :fields="fields" :loading="busy" @search="load" />
    <DataTable :rows="rows" :columns="columns" :loading="busy" empty-label="暂无审计记录。" filename="audit.csv" @refresh="load" />
  </section>
</template>
