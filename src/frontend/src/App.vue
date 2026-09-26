<script setup lang="ts">
import { ref } from 'vue'
import LoginPage from './pages/LoginPage.vue'
import PlannerPage from './pages/PlannerPage.vue'
import TasksPage from './pages/TasksPage.vue'
import NodeManagementPage from './pages/NodeManagementPage.vue'
import HumanInterventionsPage from './pages/HumanInterventionsPage.vue'
import AuditPage from './pages/AuditPage.vue'
import LlmUsagePage from './pages/LlmUsagePage.vue'
import UserManagementPage from './pages/UserManagementPage.vue'
import BusinessResources from './components/resources/BusinessResources.vue'
import PermissionCenter from './components/permissions/PermissionCenter.vue'
import TaskApprovalCenter from './components/task/TaskApprovalCenter.vue'
import TaskReconciliationCenter from './components/task/TaskReconciliationCenter.vue'
import WorkflowDesigner from './components/workflow/WorkflowDesigner.vue'

const dark = ref(localStorage.getItem('agentrpa-theme') !== 'light')
const token = ref(sessionStorage.getItem('agentrpa-token') || '')
const admin = ref(sessionStorage.getItem('agentrpa-admin') === 'true')
const userName = ref(sessionStorage.getItem('agentrpa-user') || '')
const nav = ref('AI 工作台')

function toggleTheme() {
  dark.value = !dark.value
  localStorage.setItem('agentrpa-theme', dark.value ? 'dark' : 'light')
}
function authenticated(session: { accessToken: string; userName: string; roles: string[] }) {
  token.value = session.accessToken
  admin.value = session.roles.includes('Admin')
  userName.value = session.userName
  sessionStorage.setItem('agentrpa-token', token.value)
  sessionStorage.setItem('agentrpa-user', userName.value)
  sessionStorage.setItem('agentrpa-admin', String(admin.value))
}
function logout() {
  token.value = ''
  admin.value = false
  sessionStorage.removeItem('agentrpa-token')
  sessionStorage.removeItem('agentrpa-user')
  sessionStorage.removeItem('agentrpa-admin')
}
</script>

<template>
  <main :class="dark ? 'theme-dark' : 'theme-light'" class="app-shell">
    <aside class="sidebar">
      <div class="brand"><span class="brand-mark">AR</span><div><b>AgentRPA</b><small>Automation Control Plane</small></div></div>
      <nav v-if="token">
        <button v-for="item in ['AI 工作台', '任务中心', '城市与系统', '人工介入', 'LLM 用量']" :key="item" :class="{ active: nav === item }" @click="nav = item">{{ item }}</button>
        <button v-if="admin" :class="{ active: nav === '权限中心' }" @click="nav = '权限中心'">权限中心</button>
        <button v-if="admin" :class="{ active: nav === '审批中心' }" @click="nav = '审批中心'">审批中心</button>
        <button v-if="admin" :class="{ active: nav === '核验中心' }" @click="nav = '核验中心'">核验中心</button>
        <button v-if="admin" :class="{ active: nav === 'Workflow 管理' }" @click="nav = 'Workflow 管理'">Workflow 管理</button>
        <button v-if="admin" :class="{ active: nav === '节点管理' }" @click="nav = '节点管理'">节点管理</button>
        <button v-if="admin" :class="{ active: nav === '审计记录' }" @click="nav = '审计记录'">审计记录</button>
        <button v-if="admin" :class="{ active: nav === '账户与角色' }" @click="nav = '账户与角色'">账户与角色</button>
      </nav>
      <div class="sidebar-foot">{{ token ? `已登录：${userName}` : '请登录后继续' }}</div>
    </aside>
    <section class="workspace">
      <header class="topbar"><div><span class="eyebrow">CONTROL CENTER</span><h1>{{ token ? nav : '登录' }}</h1></div><div class="actions"><button class="icon-btn" aria-label="切换主题" @click="toggleTheme">{{ dark ? '☼' : '☾' }}</button><button v-if="token" class="action-btn" @click="logout">退出登录</button></div></header>
      <div class="content">
        <LoginPage v-if="!token" @authenticated="authenticated" />
        <template v-else>
          <PlannerPage v-if="nav === 'AI 工作台'" :token="token" @submitted="nav = '任务中心'" />
          <BusinessResources v-else-if="nav === '城市与系统'" :token="token" :admin="admin" />
          <PermissionCenter v-else-if="nav === '权限中心' && admin" :token="token" />
          <TaskApprovalCenter v-else-if="nav === '审批中心' && admin" :token="token" />
          <TaskReconciliationCenter v-else-if="nav === '核验中心' && admin" :token="token" />
          <WorkflowDesigner v-else-if="nav === 'Workflow 管理' && admin" :token="token" />
          <NodeManagementPage v-else-if="nav === '节点管理' && admin" :token="token" />
          <HumanInterventionsPage v-else-if="nav === '人工介入'" :token="token" />
          <LlmUsagePage v-else-if="nav === 'LLM 用量'" :token="token" />
          <AuditPage v-else-if="nav === '审计记录' && admin" :token="token" />
          <UserManagementPage v-else-if="nav === '账户与角色' && admin" :token="token" />
          <TasksPage v-else :key="nav" :token="token" />
        </template>
      </div>
    </section>
  </main>
</template>
