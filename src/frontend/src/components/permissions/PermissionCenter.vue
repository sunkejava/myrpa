<script setup lang="ts">
import { onMounted, ref } from 'vue'

type User = { id: string; userName: string; displayName: string; enabled: boolean }
type Resource = { id: string; code: string; name: string; enabled?: boolean }
type Policy = { id: string; subjectId: string; cityId: string; systemId: string; functionId: string; action: string; enabled: boolean; denied: boolean }
const props = defineProps<{ token: string }>()
const users = ref<User[]>([])
const cities = ref<Resource[]>([])
const systems = ref<Resource[]>([])
const functions = ref<Resource[]>([])
const policies = ref<Policy[]>([])
const subjectId = ref('')
const cityId = ref('')
const systemId = ref('')
const functionId = ref('')
const action = ref('Execute')
const error = ref('')
const notice = ref('')
const busy = ref(false)

async function call<T>(path: string, method = 'GET', body?: object): Promise<T> {
  const response = await fetch(`/api/${path}`, {
    method, headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' },
    ...(body ? { body: JSON.stringify(body) } : {})
  })
  if (!response.ok) {
    const details: unknown = await response.json().catch(() => null)
    throw new Error(details && typeof details === 'object' && 'message' in details ? String(details.message) : `请求失败 (${response.status})`)
  }
  return await response.json() as T
}
async function load() {
  try {
    const [userRows, cityRows, grants] = await Promise.all([
      call<User[]>('user-management/users'), call<Resource[]>('business-resources/cities'), call<Policy[]>('permission-policies')
    ])
    users.value = userRows
    cities.value = cityRows
    policies.value = grants
  } catch (e) { error.value = e instanceof Error ? e.message : '权限数据加载失败' }
}
async function selectCity(id: string) {
  cityId.value = id
  systemId.value = ''
  functionId.value = ''
  functions.value = []
  try { systems.value = id ? await call<Resource[]>(`business-resources/cities/${id}/systems`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '系统加载失败' }
}
async function selectSystem(id: string) {
  systemId.value = id
  functionId.value = ''
  try { functions.value = id ? await call<Resource[]>(`business-resources/systems/${id}/functions`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '功能加载失败' }
}
async function savePolicy(denied: boolean) {
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    await call(denied ? 'permission-policies/deny' : 'permission-policies', 'POST', { subjectId: subjectId.value, cityId: cityId.value,
      systemId: systemId.value, functionId: functionId.value, action: action.value })
    policies.value = await call<Policy[]>('permission-policies')
    notice.value = denied ? '已添加显式拒绝。' : '授权成功，权限仍以服务端执行检查为准。'
  } catch (e) { error.value = e instanceof Error ? e.message : '授权失败' }
  finally { busy.value = false }
}
async function revoke(id: string) {
  busy.value = true
  error.value = ''
  try {
    await call(`permission-policies/${id}/revoke`, 'POST')
    policies.value = await call<Policy[]>('permission-policies')
  } catch (e) { error.value = e instanceof Error ? e.message : '撤销失败' }
  finally { busy.value = false }
}
function userName(id: string) { return users.value.find(x => x.id === id)?.displayName || id }
onMounted(load)
</script>

<template>
  <section class="panel form-panel resource-panel">
    <h2>权限中心</h2>
    <p class="muted">授权严格绑定用户、城市、业务系统、功能和动作。选择的功能必须属于所选系统。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <form @submit.prevent="savePolicy(false)">
      <label>用户<select v-model="subjectId" required><option value="">选择用户</option><option v-for="user in users.filter(x => x.enabled)" :key="user.id" :value="user.id">{{ user.displayName }} ({{ user.userName }})</option></select></label>
      <div class="resource-grid">
        <label>城市<select :value="cityId" required @change="selectCity(($event.target as HTMLSelectElement).value)"><option value="">选择城市</option><option v-for="city in cities.filter(x => x.enabled)" :key="city.id" :value="city.id">{{ city.name }}</option></select></label>
        <label>系统<select :value="systemId" required :disabled="!cityId" @change="selectSystem(($event.target as HTMLSelectElement).value)"><option value="">选择系统</option><option v-for="system in systems.filter(x => x.enabled)" :key="system.id" :value="system.id">{{ system.name }}</option></select></label>
      </div>
      <div class="resource-grid">
        <label>功能<select v-model="functionId" required :disabled="!systemId"><option value="">选择功能</option><option v-for="fn in functions" :key="fn.id" :value="fn.id">{{ fn.name }}</option></select></label>
        <label>动作<select v-model="action"><option v-for="item in ['View', 'Execute', 'Create', 'Update', 'Delete', 'Approve', 'Manage']" :key="item">{{ item }}</option></select></label>
      </div>
      <div class="actions"><button class="action-btn primary" :disabled="busy || !subjectId || !functionId">授权</button><button type="button" class="action-btn" :disabled="busy || !subjectId || !functionId" @click="savePolicy(true)">显式拒绝</button></div>
    </form>
    <div class="table-wrap"><table><thead><tr><th>用户</th><th>城市 / 系统 / 功能</th><th>动作</th><th>状态</th><th>操作</th></tr></thead><tbody><tr v-for="policy in policies" :key="policy.id"><td>{{ userName(policy.subjectId) }}</td><td><small>{{ policy.cityId }} / {{ policy.systemId }} / {{ policy.functionId }}</small></td><td>{{ policy.action }}</td><td>{{ policy.enabled ? (policy.denied ? '显式拒绝' : '允许') : '已撤销' }}</td><td><button v-if="policy.enabled" class="action-btn" :disabled="busy" @click="revoke(policy.id)">撤销</button></td></tr></tbody></table><p v-if="!policies.length" class="muted empty">暂无授权记录。</p></div>
  </section>
</template>
