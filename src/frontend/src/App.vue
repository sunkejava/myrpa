<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import TaskOverview from './components/task/TaskOverview.vue'
import BusinessResources from './components/resources/BusinessResources.vue'
import PermissionCenter from './components/permissions/PermissionCenter.vue'
import TaskApprovalCenter from './components/task/TaskApprovalCenter.vue'
import TaskReconciliationCenter from './components/task/TaskReconciliationCenter.vue'
import WorkflowDesigner from './components/workflow/WorkflowDesigner.vue'
import TaskDetail from './components/task/TaskDetail.vue'

type Task = { id: string; name: string; status: string; approvalStatus?: string | null; total: number; succeeded: number; failed: number }
type Plan = { cityId: string; systemId: string; functionId: string; action: string; riskLevel: string; workflowId: string; workflowVersion: number; requiresConfirmation: boolean }
type PlanResponse = { success: boolean; summary: string; ambiguities: string[]; plan: Plan | null }

const dark = ref(localStorage.getItem('agentrpa-theme') !== 'light')
const token = ref(sessionStorage.getItem('agentrpa-token') || '')
const admin = ref(sessionStorage.getItem('agentrpa-admin') === 'true')
const userName = ref(sessionStorage.getItem('agentrpa-user') || '')
const password = ref('')
const instruction = ref('')
const plan = ref<PlanResponse | null>(null)
const tasks = ref<Task[]>([])
const selectedTaskId = ref('')
const error = ref('')
const busy = ref(false)
const nav = ref('AI 工作台')
const themeClass = computed(() => dark.value ? 'theme-dark' : 'theme-light')
const taskRows = computed(() => tasks.value.map(task => ({ ...task, progress: task.total ? Math.round(100 * (task.succeeded + task.failed) / task.total) : 0 })))

function toggleTheme() {
  dark.value = !dark.value
  localStorage.setItem('agentrpa-theme', dark.value ? 'dark' : 'light')
}

async function api<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(path, {
    ...options,
    headers: { 'Content-Type': 'application/json', ...(token.value ? { Authorization: `Bearer ${token.value}` } : {}), ...options.headers }
  })
  if (response.status === 401 && token.value) logout()
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null)
    const message = body && typeof body === 'object' && 'message' in body ? String(body.message) : `请求失败 (${response.status})`
    throw new Error(message)
  }
  return await response.json() as T
}

async function login() {
  busy.value = true
  error.value = ''
  try {
    const result = await api<{ accessToken: string; userName: string; roles: string[] }>('/api/auth/login', {
      method: 'POST', body: JSON.stringify({ userName: userName.value, password: password.value })
    })
    token.value = result.accessToken
    admin.value = result.roles.includes('Admin')
    userName.value = result.userName
    sessionStorage.setItem('agentrpa-token', token.value)
    sessionStorage.setItem('agentrpa-user', userName.value)
    sessionStorage.setItem('agentrpa-admin', String(admin.value))
    password.value = ''
    await loadTasks()
  } catch (e) { error.value = e instanceof Error ? e.message : '登录失败' }
  finally { busy.value = false }
}

function logout() {
  token.value = ''
  admin.value = false
  plan.value = null
  tasks.value = []
  sessionStorage.removeItem('agentrpa-token')
  sessionStorage.removeItem('agentrpa-user')
  sessionStorage.removeItem('agentrpa-admin')
}

async function loadTasks() {
  try { tasks.value = await api<Task[]>('/api/tasks') }
  catch (e) { error.value = e instanceof Error ? e.message : '任务列表加载失败' }
}
async function queueTask(id: string) {
  error.value = ''
  try {
    await api(`/api/tasks/${id}/queue`, { method: 'POST', body: '{}' })
    await loadTasks()
  } catch (e) { error.value = e instanceof Error ? e.message : '入队失败' }
}
async function retryTask(id: string) {
  error.value = ''
  try {
    await api(`/api/tasks/${id}/retry-failed`, { method: 'POST', body: '{}' })
    await loadTasks()
  } catch (e) { error.value = e instanceof Error ? e.message : '重试失败' }
}
async function cancelTask(id: string) {
  if (!window.confirm('确认取消任务？已执行的外部操作可能需要管理员核验。')) return
  error.value = ''
  try {
    await api(`/api/tasks/${id}/cancel`, { method: 'POST', body: '{}' })
    await loadTasks()
  } catch (e) { error.value = e instanceof Error ? e.message : '取消任务失败' }
}

async function makePlan() {
  busy.value = true
  error.value = ''
  plan.value = null
  try {
    plan.value = await api<PlanResponse>('/api/agent/plan', { method: 'POST', body: JSON.stringify({ instruction: instruction.value }) })
  } catch (e) { error.value = e instanceof Error ? e.message : '任务规划失败' }
  finally { busy.value = false }
}

async function execute() {
  if (!plan.value?.plan) return
  busy.value = true
  error.value = ''
  try {
    await api<{ id: string }>('/api/agent/execute', { method: 'POST', body: JSON.stringify({ instruction: instruction.value, confirmed: true }) })
    plan.value = null
    instruction.value = ''
    nav.value = '任务中心'
    await loadTasks()
  } catch (e) { error.value = e instanceof Error ? e.message : '任务提交失败' }
  finally { busy.value = false }
}

onMounted(() => { if (token.value) void loadTasks() })
</script>

<template>
  <main :class="themeClass" class="app-shell">
    <aside class="sidebar">
      <div class="brand"><span class="brand-mark">AR</span><div><b>AgentRPA</b><small>Automation Control Plane</small></div></div>
      <nav v-if="token">
        <button v-for="item in ['AI 工作台', '任务中心', '城市与系统']" :key="item" :class="{ active: nav === item }" @click="nav = item; if (item === '任务中心') loadTasks()">{{ item }}</button>
        <button v-if="admin" :class="{ active: nav === '权限中心' }" @click="nav = '权限中心'">权限中心</button>
        <button v-if="admin" :class="{ active: nav === '审批中心' }" @click="nav = '审批中心'">审批中心</button>
        <button v-if="admin" :class="{ active: nav === '核验中心' }" @click="nav = '核验中心'">核验中心</button>
        <button v-if="admin" :class="{ active: nav === 'Workflow 管理' }" @click="nav = 'Workflow 管理'">Workflow 管理</button>
      </nav>
      <div class="sidebar-foot">{{ token ? `已登录：${userName}` : '请登录后继续' }}</div>
    </aside>
    <section class="workspace">
      <header class="topbar"><div><span class="eyebrow">CONTROL CENTER</span><h1>{{ token ? nav : '登录' }}</h1></div><div class="actions"><button class="icon-btn" aria-label="切换主题" @click="toggleTheme">{{ dark ? '☼' : '☾' }}</button><button v-if="token" class="action-btn" @click="logout">退出登录</button></div></header>
      <div class="content">
        <p v-if="error" class="error" role="alert">{{ error }}</p>
        <section v-if="!token" class="panel form-panel">
          <h2>登录 AgentRPA</h2>
          <form @submit.prevent="login">
            <label>用户名<input v-model.trim="userName" required autocomplete="username" /></label>
            <label>密码<input v-model="password" type="password" required autocomplete="current-password" /></label>
            <button class="action-btn primary" :disabled="busy">{{ busy ? '登录中…' : '登录' }}</button>
          </form>
        </section>
        <template v-else>
          <section v-if="nav === 'AI 工作台'" class="panel form-panel">
            <span class="eyebrow">AGENT PLANNER</span>
            <h2>今天需要帮你处理什么？</h2>
            <p class="muted">请明确城市、业务系统和业务功能。提交前可查看规划结果及风险。</p>
            <form @submit.prevent="makePlan">
              <label>任务描述<textarea v-model.trim="instruction" required rows="5" placeholder="例如：查询青岛社保人员状态"></textarea></label>
              <button class="action-btn primary" :disabled="busy">{{ busy ? '正在处理…' : '生成计划' }}</button>
            </form>
            <div v-if="plan" class="plan-result">
              <h3>规划结果</h3><p>{{ plan.summary }}</p>
              <ul v-if="plan.ambiguities.length"><li v-for="reason in plan.ambiguities" :key="reason">{{ reason }}</li></ul>
              <dl v-if="plan.plan"><dt>城市 ID</dt><dd>{{ plan.plan.cityId }}</dd><dt>系统 ID</dt><dd>{{ plan.plan.systemId }}</dd><dt>功能 ID</dt><dd>{{ plan.plan.functionId }}</dd><dt>动作 / 风险</dt><dd>{{ plan.plan.action }} / {{ plan.plan.riskLevel }}</dd><dt>Workflow</dt><dd>{{ plan.plan.workflowId }} v{{ plan.plan.workflowVersion }}</dd></dl>
              <button v-if="plan.plan" class="action-btn primary" :disabled="busy" @click="execute">{{ plan.plan.requiresConfirmation ? '确认并提交任务' : '提交任务' }}</button>
            </div>
          </section>
          <BusinessResources v-else-if="nav === '城市与系统'" :token="token" :admin="admin" />
          <PermissionCenter v-else-if="nav === '权限中心' && admin" :token="token" />
          <TaskApprovalCenter v-else-if="nav === '审批中心' && admin" :token="token" />
          <TaskReconciliationCenter v-else-if="nav === '核验中心' && admin" :token="token" />
          <WorkflowDesigner v-else-if="nav === 'Workflow 管理' && admin" :token="token" />
          <div v-else>
            <div class="panel-title"><span>我的任务</span><button class="action-btn" @click="loadTasks">刷新</button></div>
            <TaskOverview :tasks="taskRows" :queue="queueTask" :retry="retryTask" :cancel="cancelTask" :inspect="id => selectedTaskId = id" />
            <TaskDetail v-if="selectedTaskId" :key="selectedTaskId" :task-id="selectedTaskId" :token="token" />
            <p v-if="tasks.length === 0" class="muted empty">暂无任务。可以从 AI 工作台创建任务。</p>
          </div>
        </template>
      </div>
    </section>
  </main>
</template>
