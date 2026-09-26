<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import ControlLayout from './layouts/ControlLayout.vue'
import LoginPage from './pages/LoginPage.vue'
import PlannerPage from './pages/PlannerPage.vue'
import TasksPage from './pages/TasksPage.vue'
import NodeManagementPage from './pages/NodeManagementPage.vue'
import NodePoolsPage from './pages/NodePoolsPage.vue'
import NodeCapabilitiesPage from './pages/NodeCapabilitiesPage.vue'
import DispatchMonitorPage from './pages/DispatchMonitorPage.vue'
import HumanInterventionsPage from './pages/HumanInterventionsPage.vue'
import AuditPage from './pages/AuditPage.vue'
import LlmUsagePage from './pages/LlmUsagePage.vue'
import UserManagementPage from './pages/UserManagementPage.vue'
import BusinessResources from './components/resources/BusinessResources.vue'
import PermissionCenter from './components/permissions/PermissionCenter.vue'
import TaskApprovalCenter from './components/task/TaskApprovalCenter.vue'
import TaskReconciliationCenter from './components/task/TaskReconciliationCenter.vue'
import WorkflowDesigner from './components/workflow/WorkflowDesigner.vue'
import { useSession } from './stores/session'
import { useLocale } from './locales'

const { token, userName, admin, checking, setSession, clearSession, validateSession } = useSession()
const { t } = useLocale()
const page = ref('planner')
const interventionExecutionId = ref('')
function openIntervention(executionId: string) { interventionExecutionId.value = executionId; page.value = 'interventions' }
const pages = { planner: PlannerPage, tasks: TasksPage, resources: BusinessResources, interventions: HumanInterventionsPage,
  usage: LlmUsagePage, permissions: PermissionCenter, approvals: TaskApprovalCenter, reconciliation: TaskReconciliationCenter,
  workflows: WorkflowDesigner, nodes: NodeManagementPage, pools: NodePoolsPage, capabilities: NodeCapabilitiesPage,
  dispatch: DispatchMonitorPage, audit: AuditPage, users: UserManagementPage }
const selected = computed(() => pages[page.value as keyof typeof pages] || TasksPage)
onMounted(validateSession)
</script>

<template>
  <ControlLayout :logged-in="!!token && !checking" :admin="admin" :user-name="userName" :page="page" @navigate="page = $event" @logout="clearSession">
    <p v-if="checking" role="status">{{ t('common.loading') }}</p>
    <LoginPage v-else-if="!token" @authenticated="setSession" />
    <component :is="selected" v-else :key="page" :token="token" :admin="admin" :initial-execution-id="interventionExecutionId" @intervention="openIntervention" @submitted="page = 'tasks'" />
  </ControlLayout>
</template>
