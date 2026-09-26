<script setup lang="ts">
import ThemePreferences from '../components/common/ThemePreferences.vue'
import { useLocale } from '../locales'
import { useTheme } from '../composables/useTheme'

const props = defineProps<{ loggedIn: boolean; admin: boolean; userName: string; page: string }>()
const emit = defineEmits<{ navigate: [page: string]; logout: [] }>()
const { t } = useLocale()
const { settings, style, persist } = useTheme()
const groups = [
  { label: 'system.groupWork', items: [{ id: 'planner', label: 'system.planner' }, { id: 'tasks', label: 'system.tasks' }, { id: 'interventions', label: 'system.interventions' }] },
  { label: 'system.groupAutomation', items: [{ id: 'workflows', label: 'system.workflows', admin: true }, { id: 'approvals', label: 'system.approvals', admin: true }, { id: 'reconciliation', label: 'system.reconciliation', admin: true }, { id: 'dispatch', label: 'system.dispatch', admin: true }] },
  { label: 'system.groupManagement', items: [{ id: 'resources', label: 'system.resources' }, { id: 'nodes', label: 'system.nodes', admin: true }, { id: 'pools', label: 'system.pools', admin: true }, { id: 'capabilities', label: 'system.capabilities', admin: true }, { id: 'permissions', label: 'system.permissions', admin: true }, { id: 'users', label: 'system.users', admin: true }] },
  { label: 'system.groupAnalytics', items: [{ id: 'usage', label: 'system.usage' }, { id: 'audit', label: 'system.audit', admin: true }] }
]
const activeGroup = () => groups.find(group => group.items.some(item => item.id === props.page))
const pageLabel = () => t(activeGroup()?.items.find(item => item.id === props.page)?.label || 'system.signIn')
</script>

<template>
  <main :class="`theme-${settings.mode}`" class="app-shell" :style="style" :data-card="settings.card" :data-density="settings.density" :data-motion="settings.motion ? 'on' : 'off'" :data-tech="settings.tech">
    <aside class="sidebar" v-if="loggedIn">
      <div class="brand"><span class="brand-mark">AR</span><div><b>AgentRPA</b><small>{{ t('system.brand') }}</small></div></div>
      <nav aria-label="Main navigation">
        <div v-for="group in groups" :key="group.label" class="nav-group">
          <p class="nav-group-title">{{ t(group.label) }}</p>
          <button v-for="item in group.items.filter(x => !x.admin || admin)" :key="item.id" :class="{ active: page === item.id }" :aria-current="page === item.id ? 'page' : undefined" @click="emit('navigate', item.id)">{{ t(item.label) }}</button>
        </div>
      </nav>
      <div class="sidebar-foot"><span class="user-initial">{{ userName.slice(0, 1).toUpperCase() }}</span><span><strong>{{ userName }}</strong><small>{{ t('system.loggedIn') }}</small></span></div>
    </aside>
    <section class="workspace">
      <header class="topbar"><div><span class="eyebrow">{{ loggedIn ? t(activeGroup()?.label || 'system.workspace') : t('system.brand') }}</span><h1>{{ loggedIn ? pageLabel() : t('system.signIn') }}</h1></div>
        <div class="actions"><ThemePreferences /><button class="icon-btn" :aria-label="t('system.themeToggle')" @click="settings.mode = settings.mode === 'dark' ? 'light' : 'dark'; persist()">{{ settings.mode === 'dark' ? '☼' : '☾' }}</button><button v-if="loggedIn" class="action-btn" @click="emit('logout')">{{ t('common.logout') }}</button></div>
      </header>
      <div class="content"><slot /></div>
    </section>
  </main>
</template>
