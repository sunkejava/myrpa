<script setup lang="ts">
import { computed, ref } from 'vue'
import DataTable from '../table/DataTable.vue'
import FormDialog from '../common/FormDialog.vue'
import ConfirmAction from '../common/ConfirmAction.vue'
import { useLocale } from '../../locales'
import type { UserRole } from '../../api/modules/users'

defineProps<{ roles: UserRole[]; busy: boolean }>()
const emit = defineEmits<{ create: [name: string, displayName: string]; toggle: [role: UserRole]; refresh: [] }>()
const { t } = useLocale()
const creating = ref(false)
const name = ref('')
const displayName = ref('')
const columns = computed(() => [
  { key: 'name', label: t('users.roleName'), sortable: true, filterable: true },
  { key: 'displayName', label: t('users.displayName'), sortable: true, filterable: true },
  { key: 'enabled', label: t('users.status'), format: (value: unknown) => t(value ? 'users.enabled' : 'users.disabled') }
])
function submit() { emit('create', name.value.trim(), displayName.value.trim()); name.value = ''; displayName.value = ''; creating.value = false }
</script>

<template>
  <div class="page-toolbar"><h2>{{ t('users.roles') }}</h2><button class="action-btn primary" @click="creating = true">{{ t('users.createRole') }}</button></div>
  <section class="panel"><DataTable :rows="roles" :columns="columns" :loading="busy" filename="roles.csv" @refresh="emit('refresh')"><template #actions="{ row }">
    <ConfirmAction v-if="(row as UserRole).enabled && (row as UserRole).name.toLowerCase() !== 'admin'" :label="t('users.disabled')" :title="t('users.roleDisabled')" :message="t('users.disableRoleMessage')" :busy="busy" @confirmed="emit('toggle', row as UserRole)" />
    <button v-else-if="!(row as UserRole).enabled" class="action-btn" :disabled="busy" @click="emit('toggle', row as UserRole)">{{ t('users.enabled') }}</button>
  </template></DataTable></section>
  <FormDialog :open="creating" :title="t('users.createRole')" :submit-label="t('users.createRole')" :busy="busy" @close="creating = false" @submit="submit">
    <label>{{ t('users.roleName') }}<input v-model.trim="name" required maxlength="64" /></label>
    <label>{{ t('users.displayName') }}<input v-model.trim="displayName" required maxlength="128" /></label>
  </FormDialog>
</template>
