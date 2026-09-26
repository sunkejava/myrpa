<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import DataTable from '../components/table/DataTable.vue'
import FormDialog from '../components/common/FormDialog.vue'
import DetailDrawer from '../components/common/DetailDrawer.vue'
import ConfirmAction from '../components/common/ConfirmAction.vue'
import RolesPanel from '../components/users/RolesPanel.vue'
import { userRequest, type UserAccount, type UserRole } from '../api/modules/users'
import { useLocale } from '../locales'

const props = defineProps<{ token: string }>()
const { t } = useLocale()
const tab = ref<'accounts' | 'roles'>('accounts')
const users = ref<UserAccount[]>([])
const roles = ref<UserRole[]>([])
const selectedUser = ref('')
const assignedRoles = ref<UserRole[]>([])
const roleId = ref('')
const creatingUser = ref(false)
const userName = ref('')
const displayName = ref('')
const password = ref('')
const busy = ref(false)
const error = ref('')
const notice = ref('')
const columns = computed(() => [
  { key: 'userName', label: t('users.userName'), sortable: true, filterable: true },
  { key: 'displayName', label: t('users.displayName'), sortable: true, filterable: true },
  { key: 'enabled', label: t('users.status'), format: (value: unknown) => t(value ? 'users.enabled' : 'users.disabled') }
])
const selectedName = computed(() => users.value.find(user => user.id === selectedUser.value)?.displayName || '')
const call = <T,>(path: string, method = 'GET', body?: object) => userRequest<T>(props.token, path, method, body)

async function load() {
  error.value = ''
  try { [users.value, roles.value] = await Promise.all([call<UserAccount[]>('users'), call<UserRole[]>('roles')]) }
  catch (e) { error.value = e instanceof Error ? e.message : t('users.loadFailed') }
}
async function selectUser(id: string) {
  selectedUser.value = id; roleId.value = ''; assignedRoles.value = []
  if (!id) return
  try { assignedRoles.value = await call<UserRole[]>(`users/${id}/roles`) }
  catch (e) { error.value = e instanceof Error ? e.message : t('users.roleFailed') }
}
async function run(action: () => Promise<void>, success: string) {
  busy.value = true; error.value = ''; notice.value = ''
  try { await action(); await load(); notice.value = success; return true }
  catch (e) { error.value = e instanceof Error ? e.message : t('users.actionFailed'); return false }
  finally { busy.value = false }
}
async function createUser() {
  if (await run(async () => {
    await call('users', 'POST', { userName: userName.value.trim(), displayName: displayName.value.trim(), password: password.value })
  }, t('users.userCreated'))) {
    userName.value = ''; displayName.value = ''; password.value = ''; creatingUser.value = false
  }
}
async function createRole(name: string, display: string) {
  await run(async () => { await call('roles', 'POST', { name, displayName: display }) }, t('users.roleCreated'))
}
async function assignRole() {
  if (!selectedUser.value || !roleId.value) return
  await run(async () => {
    await call(`users/${selectedUser.value}/roles/${roleId.value}`, 'POST')
    await selectUser(selectedUser.value)
  }, t('users.roleAssigned'))
}
async function removeRole(role: UserRole) {
  await run(async () => {
    await call(`users/${selectedUser.value}/roles/${role.id}`, 'DELETE')
    await selectUser(selectedUser.value)
  }, t('users.roleRemoved'))
}
async function setUserEnabled(user: UserAccount) {
  await run(async () => { await call(`users/${user.id}/enabled`, 'POST', { enabled: !user.enabled }) }, t(user.enabled ? 'users.userDisabled' : 'users.userEnabled'))
}
async function setRoleEnabled(role: UserRole) {
  await run(async () => { await call(`roles/${role.id}/enabled`, 'POST', { enabled: !role.enabled }) }, t(role.enabled ? 'users.roleDisabled' : 'users.roleEnabled'))
}
onMounted(load)
</script>

<template>
  <div class="page-toolbar"><h2>{{ t('users.title') }}</h2></div>
  <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
  <div class="page-tabs" role="tablist" :aria-label="t('users.title')">
    <button role="tab" class="action-btn" :aria-selected="tab === 'accounts'" @click="tab = 'accounts'">{{ t('users.accounts') }}</button>
    <button role="tab" class="action-btn" :aria-selected="tab === 'roles'" @click="tab = 'roles'">{{ t('users.roles') }}</button>
  </div>
  <template v-if="tab === 'accounts'">
    <div class="page-toolbar"><h2>{{ t('users.accounts') }}</h2><button class="action-btn primary" @click="creatingUser = true">{{ t('users.createUser') }}</button></div>
    <section class="panel"><DataTable :rows="users" :columns="columns" :loading="busy" filename="users.csv" @refresh="load"><template #actions="{ row }"><div class="actions">
      <button class="action-btn" @click="selectUser((row as UserAccount).id)">{{ t('users.manageRoles') }}</button>
      <ConfirmAction v-if="(row as UserAccount).enabled" :label="t('users.disabled')" :title="t('users.userDisabled')" :message="t('users.disableUserMessage')" :busy="busy" @confirmed="setUserEnabled(row as UserAccount)" />
      <button v-else class="action-btn" :disabled="busy" @click="setUserEnabled(row as UserAccount)">{{ t('users.enabled') }}</button>
    </div></template></DataTable></section>
  </template>
  <RolesPanel v-else :roles="roles" :busy="busy" @create="createRole" @toggle="setRoleEnabled" @refresh="load" />
  <FormDialog :open="creatingUser" :title="t('users.createUser')" :submit-label="t('users.createUser')" :busy="busy" @close="creatingUser = false" @submit="createUser">
    <label>{{ t('users.userName') }}<input v-model.trim="userName" required maxlength="128" autocomplete="off" /></label>
    <label>{{ t('users.displayName') }}<input v-model.trim="displayName" required maxlength="128" /></label>
    <label>{{ t('users.password') }}<input v-model="password" type="password" required autocomplete="new-password" /></label>
  </FormDialog>
  <DetailDrawer :open="!!selectedUser" :title="`${t('users.manageRoles')} · ${selectedName}`" @close="selectedUser = ''">
    <section class="panel form-panel">
      <h3>{{ t('users.members') }}</h3><p class="muted">{{ t('users.assigned') }}：{{ assignedRoles.map(role => role.displayName).join('、') || t('users.none') }}</p>
      <div v-for="role in assignedRoles" :key="role.id" class="role-row"><span>{{ role.displayName }} ({{ role.name }})</span><ConfirmAction :label="t('users.revoke')" :title="t('users.revokeTitle')" :message="t('users.revokeMessage')" :busy="busy" @confirmed="removeRole(role)" /></div>
      <form @submit.prevent="assignRole"><label>{{ t('users.role') }}<select v-model="roleId" :aria-label="t('users.chooseRole')" required><option value="">{{ t('users.chooseRole') }}</option><option v-for="role in roles.filter(item => item.enabled && !assignedRoles.some(assigned => assigned.id === item.id))" :key="role.id" :value="role.id">{{ role.displayName }}</option></select></label><button class="action-btn primary" :disabled="busy || !roleId">{{ t('users.assign') }}</button></form>
    </section>
  </DetailDrawer>
</template>
