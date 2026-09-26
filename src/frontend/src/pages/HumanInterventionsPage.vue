<script setup lang="ts">
import { onMounted, ref } from 'vue'
import HumanInterventionPanel from '../components/task/HumanInterventionPanel.vue'
import type { Intervention } from '../types/intervention'

const props = defineProps<{ token: string }>()
const rows = ref<Intervention[]>([])
const executionId = ref('')
const title = ref('')
const type = ref('ManualApproval')
const issuedToken = ref('')
const qrId = ref('')
const qrInput = ref('')
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function call<T>(path: string, method = 'GET', data?: object): Promise<T> {
  const response = await fetch(`/api/human-interventions${path}`, { method,
    headers: { Authorization: `Bearer ${props.token}`, ...(data ? { 'Content-Type': 'application/json' } : {}) },
    ...(data ? { body: JSON.stringify(data) } : {}) })
  const content = await response.text()
  let body: T | { message?: string } | null = null
  try { body = content ? JSON.parse(content) : null } catch { /* HTTP error without JSON */ }
  if (!response.ok) throw new Error(body && typeof body === 'object' && 'message' in body ? String(body.message) : `请求失败 (${response.status})`)
  return body as T
}
async function load() {
  try { rows.value = await call<Intervention[]>('') }
  catch (e) { error.value = e instanceof Error ? e.message : '人工介入加载失败' }
}
async function create() {
  busy.value = true; error.value = ''; notice.value = ''; issuedToken.value = ''
  try {
    const result = await call<Intervention>('', 'POST', { executionId: executionId.value, type: type.value,
      title: title.value, expiresAt: new Date(Date.now() + (type.value === 'QrLogin' ? 5 : 30) * 60_000).toISOString() })
    issuedToken.value = result.qrToken || ''
    notice.value = '人工介入已创建。二维码令牌只在此时返回，请妥善交给该执行的处理人员。'
    title.value = ''; await load()
  } catch (e) { error.value = e instanceof Error ? e.message : '创建失败' }
  finally { busy.value = false }
}
async function action(row: Intervention, operation: 'complete' | 'cancel') {
  busy.value = true; error.value = ''; notice.value = ''
  try { await call(`/${row.id}/${operation}`, 'POST'); await load(); notice.value = `人工介入已${operation === 'complete' ? '完成' : '取消'}。` }
  catch (e) { error.value = e instanceof Error ? e.message : '操作失败' }
  finally { busy.value = false }
}
async function consumeQr() {
  busy.value = true; error.value = ''; notice.value = ''
  try {
    await call(`/${qrId.value}/qr/consume`, 'POST', { token: qrInput.value })
    qrInput.value = ''; issuedToken.value = ''; await load()
    notice.value = '二维码令牌已消费，原执行会话可继续。'
  } catch (e) { error.value = e instanceof Error ? e.message : '二维码授权失败' }
  finally { busy.value = false }
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel">
    <div class="panel-title"><span>人工介入</span><button class="action-btn" @click="load">刷新</button></div>
    <p class="muted">人工介入仅针对本人执行实例。确认外部操作已完成后再点击“完成”，节点会恢复原执行会话。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <p v-if="issuedToken" class="muted">本次二维码令牌（只显示一次）：<code>{{ issuedToken }}</code></p>
    <form @submit.prevent="create">
      <label>执行 ID<input v-model.trim="executionId" required placeholder="从任务详情复制 Execution ID" /></label>
      <label>标题<input v-model.trim="title" required maxlength="200" /></label>
      <label>类型<select v-model="type"><option value="ManualApproval">人工确认</option><option value="QrLogin">二维码授权</option><option value="Captcha">验证码</option><option value="UKeyConfirmation">UKey 确认</option><option value="FaceAuthentication">人脸认证</option></select></label>
      <button class="action-btn primary" :disabled="busy">创建人工介入</button>
    </form>
    <form @submit.prevent="consumeQr">
      <label>二维码介入<select v-model="qrId" required><option value="">选择待扫码记录</option><option v-for="row in rows.filter(x => x.type === 'QrLogin' && x.status === 'Opened')" :key="row.id" :value="row.id">{{ row.title }} · {{ row.id }}</option></select></label>
      <label>一次性令牌<input v-model.trim="qrInput" required autocomplete="off" /></label>
      <button class="action-btn" :disabled="busy || !qrId">确认扫码完成</button>
    </form>
    <HumanInterventionPanel :rows="rows" :busy="busy" @complete="action($event, 'complete')" @cancel="action($event, 'cancel')" />
  </section>
</template>
