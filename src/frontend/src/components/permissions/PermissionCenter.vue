<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLocale } from '../../locales'
import { permissionRequest, type PermissionUser as User, type PermissionRole as Role, type PermissionResource as Resource, type UserPolicy as Policy, type RolePolicy } from '../../api/modules/permissions'
import DataTable from '../table/DataTable.vue'
import FormDialog from '../common/FormDialog.vue'

const props = defineProps<{ token: string }>()
const { t } = useLocale()
const users = ref<User[]>([])
const roles = ref<Role[]>([])
const cities = ref<Resource[]>([])
const systems = ref<Resource[]>([])
const functions = ref<Resource[]>([])
const policies = ref<Policy[]>([])
const rolePolicies = ref<RolePolicy[]>([])
const policyTarget = ref<'user' | 'role'>('user')
const subjectId = ref('')
const roleId = ref('')
const cityId = ref('')
const systemId = ref('')
const functionId = ref('')
const action = ref('Execute')
const error = ref('')
const notice = ref('')
const busy = ref(false)
const granting = ref(false)
type GrantRow = { id: string; kind: 'user' | 'role'; subject: string; scope: string; action: string; status: string; enabled: boolean }
const rows = computed<GrantRow[]>(() => [
  ...policies.value.map(policy => ({ id: policy.id, kind: 'user' as const, subject: `${t('permission.user')} · ${userName(policy.subjectId)}`, scope: `${policy.cityId} / ${policy.systemId} / ${policy.functionId}`, action: policy.action, status: t(policy.enabled ? (policy.denied ? 'permission.deny' : 'permission.allow') : 'permission.revoked'), enabled: policy.enabled })),
  ...rolePolicies.value.map(policy => ({ id: policy.id, kind: 'role' as const, subject: `${t('permission.role')} · ${roleName(policy.roleId)}`, scope: `${policy.cityId} / ${policy.systemId} / ${policy.functionId}`, action: policy.action, status: t(policy.enabled ? (policy.denied ? 'permission.deny' : 'permission.allow') : 'permission.revoked'), enabled: policy.enabled }))
])
const columns = computed(() => [
  { key: 'subject', label: t('permission.subject'), sortable: true, filterable: true },
  { key: 'scope', label: t('permission.scope'), filterable: true },
  { key: 'action', label: t('permission.action'), sortable: true },
  { key: 'status', label: t('permission.status'), sortable: true }
])

const call = <T,>(path: string, method = 'GET', body?: object) => permissionRequest<T>(props.token, path, method, body)
async function load() {
  try {
    const [userRows, roleRows, cityRows, grants, roleGrants] = await Promise.all([
      call<User[]>('user-management/users'), call<Role[]>('user-management/roles'), call<Resource[]>('business-resources/cities'),
      call<Policy[]>('permission-policies'), call<RolePolicy[]>('role-policies')
    ])
    users.value = userRows
    roles.value = roleRows
    cities.value = cityRows
    policies.value = grants
    rolePolicies.value = roleGrants
  } catch (e) { error.value = e instanceof Error ? e.message : t('permission.loadFailed') }
}
async function selectCity(id: string) {
  cityId.value = id
  systemId.value = ''
  functionId.value = ''
  functions.value = []
  try { systems.value = id ? await call<Resource[]>(`business-resources/cities/${id}/systems`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : t('permission.systemFailed') }
}
async function selectSystem(id: string) {
  systemId.value = id
  functionId.value = ''
  try { functions.value = id ? await call<Resource[]>(`business-resources/systems/${id}/functions`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : t('permission.functionFailed') }
}
async function savePolicy(denied: boolean) {
  busy.value = true
  error.value = ''
  notice.value = ''
  try {
    if (policyTarget.value === 'role') {
      await call('role-policies', 'POST', { roleId: roleId.value, cityId: cityId.value,
        systemId: systemId.value, functionId: functionId.value, action: action.value, denied })
      rolePolicies.value = await call<RolePolicy[]>('role-policies')
    } else {
      await call(denied ? 'permission-policies/deny' : 'permission-policies', 'POST', { subjectId: subjectId.value, cityId: cityId.value,
        systemId: systemId.value, functionId: functionId.value, action: action.value })
      policies.value = await call<Policy[]>('permission-policies')
    }
    notice.value = t(denied ? 'permission.denied' : 'permission.granted')
    granting.value = false
  } catch (e) { error.value = e instanceof Error ? e.message : t('permission.grantFailed') }
  finally { busy.value = false }
}
async function revoke(id: string, target: 'user' | 'role') {
  busy.value = true
  error.value = ''
  try {
    await call(`${target === 'role' ? 'role-policies' : 'permission-policies'}/${id}/revoke`, 'POST')
    if (target === 'role') rolePolicies.value = await call<RolePolicy[]>('role-policies')
    else policies.value = await call<Policy[]>('permission-policies')
  } catch (e) { error.value = e instanceof Error ? e.message : t('permission.revokeFailed') }
  finally { busy.value = false }
}
function userName(id: string) { return users.value.find(x => x.id === id)?.displayName || id }
function roleName(id: string) { return roles.value.find(x => x.id === id)?.displayName || id }
onMounted(load)
</script>

<template>
  <div class="page-toolbar"><div><h2>{{ t('permission.title') }}</h2><p class="muted">{{ t('permission.help') }}</p></div><button class="action-btn primary" @click="granting = true">{{ t('permission.newGrant') }}</button></div>
  <p v-if="error" class="error" role="alert">{{ error }}</p><p v-if="notice" class="muted" role="status">{{ notice }}</p>
  <section class="panel"><div class="panel-title"><h3>{{ t('permission.records') }}</h3></div><DataTable :rows="rows" :columns="columns" :empty-label="t('permission.empty')" filename="permissions.csv" @refresh="load"><template #actions="{ row }"><button v-if="(row as GrantRow).enabled" class="action-btn" :disabled="busy" @click="revoke((row as GrantRow).id, (row as GrantRow).kind)">{{ t('permission.revoke') }}</button></template></DataTable></section>
  <FormDialog :open="granting" :title="t('permission.newGrant')" :submit-label="t('permission.grant')" :busy="busy || !(policyTarget === 'role' ? roleId : subjectId) || !functionId" @close="granting = false" @submit="savePolicy(false)">
      <label>{{ t('permission.target') }}<select v-model="policyTarget"><option value="user">{{ t('permission.user') }}</option><option value="role">{{ t('permission.role') }}</option></select></label>
      <label v-if="policyTarget === 'user'">{{ t('permission.user') }}<select v-model="subjectId" required><option value="">{{ t('permission.chooseUser') }}</option><option v-for="user in users.filter(x => x.enabled)" :key="user.id" :value="user.id">{{ user.displayName }} ({{ user.userName }})</option></select></label>
      <label v-else>{{ t('permission.role') }}<select v-model="roleId" required><option value="">{{ t('permission.chooseRole') }}</option><option v-for="role in roles.filter(x => x.enabled)" :key="role.id" :value="role.id">{{ role.displayName }} ({{ role.name }})</option></select></label>
      <div class="resource-grid">
        <label>{{ t('permission.city') }}<select :value="cityId" required @change="selectCity(($event.target as HTMLSelectElement).value)"><option value="">{{ t('permission.chooseCity') }}</option><option v-for="city in cities.filter(x => x.enabled)" :key="city.id" :value="city.id">{{ city.name }}</option></select></label>
        <label>{{ t('permission.system') }}<select :value="systemId" required :disabled="!cityId" @change="selectSystem(($event.target as HTMLSelectElement).value)"><option value="">{{ t('permission.chooseSystem') }}</option><option v-for="system in systems.filter(x => x.enabled)" :key="system.id" :value="system.id">{{ system.name }}</option></select></label>
      </div>
      <div class="resource-grid">
        <label>{{ t('permission.function') }}<select v-model="functionId" required :disabled="!systemId"><option value="">{{ t('permission.chooseFunction') }}</option><option v-for="fn in functions" :key="fn.id" :value="fn.id">{{ fn.name }}</option></select></label>
        <label>{{ t('permission.action') }}<select v-model="action"><option v-for="item in ['View', 'Execute', 'Create', 'Update', 'Delete', 'Approve', 'Manage']" :key="item">{{ item }}</option></select></label>
      </div>
      <button type="button" class="action-btn" :disabled="busy || !(policyTarget === 'role' ? roleId : subjectId) || !functionId" @click="savePolicy(true)">{{ t('permission.deny') }}</button>
  </FormDialog>
</template>
