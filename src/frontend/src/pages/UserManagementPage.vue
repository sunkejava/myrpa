<script setup lang="ts">
import { onMounted, ref } from 'vue'
import DataTable from '../components/table/DataTable.vue'

type User = { id: string; userName: string; displayName: string; enabled: boolean }
type Role = { id: string; name: string; displayName: string; enabled: boolean }
const props = defineProps<{ token: string }>()
const users = ref<User[]>([])
const roles = ref<Role[]>([])
const selectedUser = ref('')
const assignedRoles = ref<Role[]>([])
const roleId = ref('')
const userName = ref('')
const displayName = ref('')
const password = ref('')
const roleName = ref('')
const roleDisplayName = ref('')
const busy = ref(false)
const error = ref('')
const notice = ref('')
const columns = [{ key: 'userName', label: '账户', sortable: true, filterable: true },
  { key: 'displayName', label: '名称', sortable: true, filterable: true },
  { key: 'enabled', label: '状态', format: (value: unknown) => value ? '启用' : '停用' }]

async function call<T>(path: string, method = 'GET', data?: object): Promise<T> {
  const response = await fetch(`/api/user-management/${path}`, { method,
    headers: { Authorization: `Bearer ${props.token}`, ...(data ? { 'Content-Type': 'application/json' } : {}) },
    ...(data ? { body: JSON.stringify(data) } : {}) })
  const content = await response.text()
  let result: T | { message?: string } | null = null
  try { result = content ? JSON.parse(content) : null } catch { /* 业务接口可能返回空正文 */ }
  if (!response.ok) throw new Error(result && typeof result === 'object' && 'message' in result ? String(result.message) : `请求失败 (${response.status})`)
  return result as T
}
async function load() {
  try { [users.value, roles.value] = await Promise.all([call<User[]>('users'), call<Role[]>('roles')]) }
  catch (e) { error.value = e instanceof Error ? e.message : '账户数据加载失败' }
}
async function selectUser(id: string) {
  selectedUser.value = id; roleId.value = ''; assignedRoles.value = []
  if (!id) return
  try { assignedRoles.value = await call<Role[]>(`users/${id}/roles`) }
  catch (e) { error.value = e instanceof Error ? e.message : '角色加载失败' }
}
async function run(action: () => Promise<void>, success: string) {
  busy.value = true; error.value = ''; notice.value = ''
  try { await action(); await load(); notice.value = success }
  catch (e) { error.value = e instanceof Error ? e.message : '操作失败' }
  finally { busy.value = false }
}
async function createUser() {
  await run(async () => {
    await call('users', 'POST', { userName: userName.value, displayName: displayName.value, password: password.value })
    userName.value = ''; displayName.value = ''; password.value = ''
  }, '账户已创建。')
}
async function createRole() {
  await run(async () => {
    await call('roles', 'POST', { name: roleName.value, displayName: roleDisplayName.value })
    roleName.value = ''; roleDisplayName.value = ''
  }, '角色已创建。')
}
async function assignRole() {
  await run(async () => {
    await call(`users/${selectedUser.value}/roles/${roleId.value}`, 'POST')
    await selectUser(selectedUser.value)
  }, '角色已分配。')
}
async function setEnabled(user: User) {
  await run(async () => { await call(`users/${user.id}/enabled`, 'POST', { enabled: !user.enabled }) }, user.enabled ? '账户已停用。' : '账户已启用。')
}
onMounted(load)
</script>

<template>
  <section class="panel form-panel">
    <div class="panel-title"><span>账户与角色</span><button class="action-btn" @click="load">刷新</button></div>
    <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
    <form @submit.prevent="createUser"><h3>创建账户</h3><div class="resource-grid"><label>登录名<input v-model.trim="userName" required /></label><label>显示名称<input v-model.trim="displayName" required /></label></div><label>初始密码<input v-model="password" type="password" required autocomplete="new-password" /></label><button class="action-btn primary" :disabled="busy">创建账户</button></form>
    <form @submit.prevent="createRole"><h3>创建角色</h3><div class="resource-grid"><label>角色标识<input v-model.trim="roleName" required /></label><label>显示名称<input v-model.trim="roleDisplayName" required /></label></div><button class="action-btn" :disabled="busy">创建角色</button></form>
    <form @submit.prevent="assignRole"><h3>分配角色</h3><div class="resource-grid"><label>账户<select aria-label="选择账户" :value="selectedUser" required @change="selectUser(($event.target as HTMLSelectElement).value)"><option value="">选择账户</option><option v-for="user in users" :key="user.id" :value="user.id">{{ user.displayName }} ({{ user.userName }})</option></select></label><label>角色<select aria-label="选择角色" v-model="roleId" required><option value="">选择角色</option><option v-for="role in roles.filter(x => x.enabled && !assignedRoles.some(assigned => assigned.id === x.id))" :key="role.id" :value="role.id">{{ role.displayName }}</option></select></label></div>
      <p v-if="selectedUser" class="muted">已分配：{{ assignedRoles.map(role => role.displayName).join('、') || '无' }}</p><button class="action-btn" :disabled="busy || !selectedUser || !roleId">分配角色</button></form>
    <DataTable :rows="users" :columns="columns" :loading="busy" filename="users.csv" @refresh="load"><template #actions="{ row }"><button class="action-btn" :disabled="busy" @click="setEnabled(row as User)">{{ (row as User).enabled ? '停用' : '启用' }}</button></template></DataTable>
  </section>
</template>
