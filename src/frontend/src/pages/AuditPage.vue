<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Entry = { id: string; createdAt: string; actor: string; action: string; resource: string; resourceId: string; result: string; summary: string }
const props = defineProps<{ token: string }>()
const rows = ref<Entry[]>([])
const actor = ref('')
const resource = ref('')
const error = ref('')
async function load() {
  error.value = ''
  try {
    const query = new URLSearchParams({ limit: '100' })
    if (actor.value.trim()) query.set('actor', actor.value.trim())
    if (resource.value.trim()) query.set('resource', resource.value.trim())
    const response = await fetch(`/api/audit?${query}`, { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`审计查询失败 (${response.status})`)
    rows.value = await response.json() as Entry[]
  } catch (e) { error.value = e instanceof Error ? e.message : '审计查询失败' }
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel">
    <h2>审计记录</h2><p v-if="error" class="error" role="alert">{{ error }}</p>
    <form @submit.prevent="load"><div class="resource-grid"><label>操作者<input v-model.trim="actor" placeholder="精确匹配" /></label><label>资源<input v-model.trim="resource" placeholder="精确匹配" /></label></div><button class="action-btn primary">查询最近 100 条</button></form>
    <div class="table-wrap"><table><thead><tr><th>时间</th><th>操作者</th><th>动作 / 资源</th><th>结果</th><th>说明</th></tr></thead><tbody>
      <tr v-for="row in rows" :key="row.id"><td>{{ new Date(row.createdAt).toLocaleString('zh-CN') }}</td><td>{{ row.actor }}</td><td>{{ row.action }}<small>{{ row.resource }} · {{ row.resourceId }}</small></td><td>{{ row.result }}</td><td>{{ row.summary }}</td></tr>
    </tbody></table><p v-if="!rows.length" class="muted empty">暂无审计记录。</p></div>
  </section>
</template>
