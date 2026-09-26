<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { useLocale } from '../../locales'

type TableColumn = { key: string; label: string; sortable?: boolean; filterable?: boolean; hidden?: boolean; format?: (value: unknown, row: object) => string }
const props = withDefaults(defineProps<{ rows: object[]; columns: TableColumn[]; rowKey?: string; loading?: boolean; pageSize?: number; density?: 'compact' | 'standard' | 'comfortable'; selectable?: boolean; emptyLabel?: string; filename?: string }>(),
  { rowKey: 'id', pageSize: 20, density: 'standard', emptyLabel: '暂无数据', filename: 'export.csv' })
const emit = defineEmits<{ refresh: []; selectionChange: [keys: string[]]; bulkAction: [keys: string[]] }>()
const { t } = useLocale()
const page = ref(1)
const sortKey = ref('')
const descending = ref(false)
const filters = ref<Record<string, string>>({})
const hidden = ref<string[]>([])
const selected = ref<string[]>([])
const visibleColumns = computed(() => props.columns.filter(x => !x.hidden && !hidden.value.includes(x.key)))
const valueOf = (row: object, key: string) => (row as Record<string, unknown>)[key]
const keyOf = (row: object) => String(valueOf(row, props.rowKey))
const display = (row: object, column: TableColumn) => column.format?.(valueOf(row, column.key), row) ?? String(valueOf(row, column.key) ?? '—')
const filtered = computed(() => {
  const matches = props.rows.filter(row => Object.entries(filters.value).every(([key, value]) => !value ||
    String(valueOf(row, key) ?? '').toLocaleLowerCase().includes(value.toLocaleLowerCase())))
  if (!sortKey.value) return matches
  const direction = descending.value ? -1 : 1
  return [...matches].sort((a, b) => String(valueOf(a, sortKey.value) ?? '').localeCompare(String(valueOf(b, sortKey.value) ?? ''), undefined, { numeric: true }) * direction)
})
const pageCount = computed(() => Math.max(1, Math.ceil(filtered.value.length / props.pageSize)))
const paged = computed(() => filtered.value.slice((page.value - 1) * props.pageSize, page.value * props.pageSize))
watch([filtered, () => props.pageSize], () => { page.value = Math.min(page.value, pageCount.value) })
function toggleSort(key: string) { descending.value = sortKey.value === key ? !descending.value : false; sortKey.value = key }
function toggle(key: string) { selected.value = selected.value.includes(key) ? selected.value.filter(x => x !== key) : [...selected.value, key]; emit('selectionChange', selected.value) }
function togglePage() {
  const keys = paged.value.map(keyOf)
  selected.value = keys.every(key => selected.value.includes(key)) ? selected.value.filter(key => !keys.includes(key)) : [...new Set([...selected.value, ...keys])]
  emit('selectionChange', selected.value)
}
function exportCsv() {
  const escape = (value: string) => `"${(/^[\s]*[=+@-]/.test(value) ? `'${value}` : value).replaceAll('"', '""')}"`
  const rows = [visibleColumns.value.map(x => escape(x.label)).join(','),
    ...filtered.value.map(row => visibleColumns.value.map(column => escape(display(row, column))).join(','))]
  const url = URL.createObjectURL(new Blob(['\uFEFF', rows.join('\r\n')], { type: 'text/csv;charset=utf-8' }))
  const anchor = document.createElement('a'); anchor.href = url; anchor.download = props.filename; anchor.click()
  window.setTimeout(() => URL.revokeObjectURL(url), 10_000)
}
</script>

<template>
  <section class="data-table" :data-density="density" :aria-busy="loading">
    <div class="data-table-toolbar actions"><button class="action-btn" type="button" :disabled="loading" @click="emit('refresh')">{{ t('common.refresh') }}</button><button class="action-btn" type="button" :disabled="!filtered.length" @click="exportCsv">{{ t('common.export') }}</button>
      <button v-if="selectable && selected.length" class="action-btn" type="button" @click="emit('bulkAction', selected)">{{ t('common.bulk') }}（{{ selected.length }}）</button>
      <details><summary class="action-btn">{{ t('common.columns') }}</summary><div class="column-picker"><label v-for="column in columns" :key="column.key"><input type="checkbox" :checked="!hidden.includes(column.key)" @change="hidden = hidden.includes(column.key) ? hidden.filter(key => key !== column.key) : [...hidden, column.key]" />{{ column.label }}</label></div></details>
    </div>
    <div class="table-wrap"><table><thead><tr><th v-if="selectable"><input :aria-label="t('common.selectPage')" type="checkbox" :checked="!!paged.length && paged.every(row => selected.includes(keyOf(row)))" @change="togglePage" /></th>
      <th v-for="column in visibleColumns" :key="column.key"><button v-if="column.sortable" type="button" class="table-sort" @click="toggleSort(column.key)">{{ column.label }} {{ sortKey === column.key ? (descending ? '↓' : '↑') : '' }}</button><span v-else>{{ column.label }}</span><input v-if="column.filterable" v-model="filters[column.key]" :aria-label="`${t('common.filter')}${column.label}`" :placeholder="`${t('common.filter')}${column.label}`" @input="page = 1" /></th><th v-if="$slots.actions">{{ t('common.actions') }}</th></tr></thead>
      <tbody><tr v-for="row in paged" :key="keyOf(row)"><td v-if="selectable"><input type="checkbox" :aria-label="`选择${keyOf(row)}`" :checked="selected.includes(keyOf(row))" @change="toggle(keyOf(row))" /></td><td v-for="column in visibleColumns" :key="column.key"><slot :name="`cell-${column.key}`" :row="row" :value="valueOf(row, column.key)">{{ display(row, column) }}</slot></td><td v-if="$slots.actions"><slot name="actions" :row="row" /></td></tr></tbody></table>
      <p v-if="loading" role="status" class="muted empty">{{ t('common.busy') }}</p><p v-else-if="!filtered.length" class="muted empty">{{ emptyLabel }}</p></div>
    <div v-if="pageCount > 1" class="data-table-footer actions"><span>{{ filtered.length }} · {{ page }} / {{ pageCount }}</span><button type="button" class="action-btn" :disabled="page === 1" @click="page--">{{ t('common.previous') }}</button><button type="button" class="action-btn" :disabled="page === pageCount" @click="page++">{{ t('common.next') }}</button></div>
  </section>
</template>
