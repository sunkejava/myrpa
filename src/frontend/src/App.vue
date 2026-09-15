<script setup lang="ts">
import { computed, ref } from 'vue'
import NodeOverview from './components/node/NodeOverview.vue'
import TaskOverview from './components/task/TaskOverview.vue'

const dark = ref(true)
const nav = ref('总览')
const themeClass = computed(() => dark.value ? 'theme-dark' : 'theme-light')

const nodes = [
  { name: 'Win-北京-01', kind: 'Physical', os: 'Windows', pool: '北京政务网', slots: '3 / 4', status: 'Online' },
  { name: 'Win-北京-VM02', kind: 'VirtualMachine', os: 'Windows', pool: '北京政务网', slots: '1 / 4', status: 'Online' },
  { name: 'Linux-Worker03', kind: 'Container', os: 'Linux', pool: '普通互联网', slots: '0 / 8', status: 'Offline' }
]
const tasks = [
  { id: 'T-20260915-001', name: '北京社保在职状态核验', progress: 72, status: 'Running', success: 68, failed: 4 },
  { id: 'T-20260915-002', name: '上海人员目录同步', progress: 100, status: 'Succeeded', success: 120, failed: 0 }
]
</script>

<template>
  <main :class="themeClass" class="app-shell">
    <aside class="sidebar">
      <div class="brand"><span class="brand-mark">AR</span><div><b>AgentRPA</b><small>Automation Control Plane</small></div></div>
      <nav>
        <button v-for="item in ['总览', '任务中心', 'Workflow', '执行节点', '人工介入', '审计与统计']" :key="item" :class="{ active: nav === item }" @click="nav = item">{{ item }}</button>
      </nav>
      <div class="sidebar-foot"><span class="online-dot"></span> Server Online</div>
    </aside>
    <section class="workspace">
      <header class="topbar"><div><span class="eyebrow">CONTROL CENTER</span><h1>{{ nav }}</h1></div><div class="actions"><button class="icon-btn" @click="dark = !dark">{{ dark ? '☼' : '☾' }}</button><button class="avatar">A</button></div></header>
      <div class="content">
        <div class="hero"><div><span class="eyebrow">AUTOMATION FABRIC</span><h2>把自然语言变成可审计的自动化执行</h2><p>Agent 负责理解与规划，Workflow 负责确定性执行，NodeAgent 负责本地浏览器、桌面与硬件能力。</p></div><div class="hero-metric"><strong>4</strong><span>Active Workers</span></div></div>
        <div class="metric-grid"><article><small>待执行任务</small><strong>12</strong><span>队列稳定</span></article><article><small>在线节点</small><strong>8</strong><span>100% 心跳正常</span></article><article><small>今日成功率</small><strong>98.7%</strong><span>+1.8% vs yesterday</span></article><article><small>人工介入</small><strong>3</strong><span>需要处理</span></article></div>
        <NodeOverview :nodes="nodes" />
        <TaskOverview :tasks="tasks" />
      </div>
    </section>
  </main>
</template>
