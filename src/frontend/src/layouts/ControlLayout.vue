<script setup lang="ts">
import ThemePreferences from '../components/common/ThemePreferences.vue'
import { useLocale } from '../locales'
import { useTheme } from '../composables/useTheme'

const props = defineProps<{ loggedIn: boolean; admin: boolean; userName: string; page: string }>()
const emit = defineEmits<{ navigate: [page: string]; logout: [] }>()
const { t } = useLocale()
const { settings, style, persist } = useTheme()
const menu = [
  { id: 'planner', label: 'system.planner' }, { id: 'tasks', label: 'system.tasks' },
  { id: 'resources', label: 'system.resources' }, { id: 'interventions', label: 'system.interventions' },
  { id: 'usage', label: 'system.usage' }, { id: 'permissions', label: 'system.permissions', admin: true },
  { id: 'approvals', label: 'system.approvals', admin: true }, { id: 'reconciliation', label: 'system.reconciliation', admin: true },
  { id: 'workflows', label: 'system.workflows', admin: true }, { id: 'nodes', label: 'system.nodes', admin: true },
  { id: 'pools', label: 'system.pools', admin: true }, { id: 'capabilities', label: 'system.capabilities', admin: true },
  { id: 'dispatch', label: 'system.dispatch', admin: true }, { id: 'audit', label: 'system.audit', admin: true },
  { id: 'users', label: 'system.users', admin: true }
]
const pageLabel = () => t(menu.find(x => x.id === props.page)?.label || 'system.signIn')
</script>

<template>
  <main :class="`theme-${settings.mode}`" class="app-shell" :style="style" :data-card="settings.card" :data-density="settings.density" :data-motion="settings.motion ? 'on' : 'off'" :data-tech="settings.tech">
    <aside class="sidebar">
      <div class="brand"><span class="brand-mark">AR</span><div><b>AgentRPA</b><small>{{ t('system.brand') }}</small></div></div>
      <nav v-if="loggedIn" aria-label="Main navigation">
        <button v-for="item in menu.filter(x => !x.admin || admin)" :key="item.id" :class="{ active: page === item.id }" @click="emit('navigate', item.id)">{{ t(item.label) }}</button>
      </nav>
      <div class="sidebar-foot">{{ loggedIn ? `${t('system.loggedIn')}：${userName}` : t('system.signIn') }}</div>
    </aside>
    <section class="workspace">
      <header class="topbar"><div><span class="eyebrow">CONTROL CENTER</span><h1>{{ loggedIn ? pageLabel() : t('system.signIn') }}</h1></div>
        <div class="actions"><ThemePreferences /><button class="icon-btn" :aria-label="t('system.themeToggle')" @click="settings.mode = settings.mode === 'dark' ? 'light' : 'dark'; persist()">{{ settings.mode === 'dark' ? '☼' : '☾' }}</button><button v-if="loggedIn" class="action-btn" @click="emit('logout')">{{ t('common.logout') }}</button></div>
      </header>
      <div class="content"><slot /></div>
    </section>
  </main>
</template>
