<script setup lang="ts">
import { onMounted, ref } from 'vue'

type Approval = { id: string; taskId: string; requesterId: string; reviewerId?: string; status: string; reason?: string; createdAt: string }
const props = defineProps<{ token: string }>()
const rows = ref<Approval[]>([])
const reasons = ref<Record<string, string>>({})
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function request<T>(path: string, method = 'GET', body?: object): Promise<T> {
  const response = await fetch(`/api/task-approvals${path}`, {
    method,
    headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' },
    ...(body ? { body: JSON.stringify(body) } : {})
  })
  if (!response.ok) {
    const details = await response.json().catch(() => null) as { message?: string } | null
    throw new Error(details?.message || `请求失败 (${response.status})`)
  }
  return await response.json() as T
}
async function load() {
  try { rows.value = await request<Approval[]>('') }
  catch (e) { error.value = e instanceof Error ? e.message : '加载审批记录失败' }
}
async function decide(id: string, approved: boolean) {
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    await request(`/${id}/decide`, 'POST', { approved, reason: reasons.value[id] || null })
    notice.value = approved ? '已批准，等待发起人入队。' : '已拒绝任务。'
    await load()
  } catch (e) { error.value = e instanceof Error ? e.message : '审批失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>任务审批</span><button class="action-btn" @click="load">刷新</button></div>
    <p class="muted">高风险任务需由另一名管理员审批；批准后由发起人在任务中心入队。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" role="status">{{ notice }}</p>
    <div class="table-wrap"><table><thead><tr><th>任务 ID</th><th>发起人 ID</th><th>状态</th><th>审批意见</th><th>操作</th></tr></thead>
      <tbody><tr v-for="item in rows" :key="item.id">
        <td><small>{{ item.taskId }}</small></td><td><small>{{ item.requesterId }}</small></td><td>{{ item.status }}</td>
        <td><input v-if="item.status === 'Pending'" v-model="reasons[item.id]" aria-label="审批意见" maxlength="1000" placeholder="拒绝时必填" /><span v-else>{{ item.reason || '—' }}</span></td>
        <td><div v-if="item.status === 'Pending'" class="actions"><button class="action-btn primary" :disabled="busy" @click="decide(item.id, true)">批准</button><button class="action-btn" :disabled="busy || !reasons[item.id]?.trim()" @click="decide(item.id, false)">拒绝</button></div></td>
      </tr></tbody></table><p v-if="!rows.length" class="muted empty">暂无审批记录。</p></div>
  </section>
</template>
