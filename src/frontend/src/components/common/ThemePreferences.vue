<script setup lang="ts">
import { ref } from 'vue'
import { useTheme } from '../../composables/useTheme'
import { useLocale } from '../../locales'
const { settings, persist } = useTheme()
const { language, t, setLanguage, importLanguagePack } = useLocale()
const error = ref('')
const open = ref(false)
async function loadPack(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  error.value = ''
  try { await importLanguagePack(file) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : t('theme.invalidPack') }
}
</script>

<template>
  <div class="theme-settings">
    <button type="button" class="action-btn" :aria-expanded="open" @click="open = !open">{{ t('common.theme') }}</button>
    <div v-if="open" class="theme-settings-panel" @change="persist">
      <label>{{ t('common.language') }}<select :aria-label="t('common.language')" :value="language" @change="setLanguage(($event.target as HTMLSelectElement).value as 'zh' | 'en' | 'custom')"><option value="zh">简体中文</option><option value="en">English</option><option value="custom">Custom</option></select></label>
      <label>{{ t('common.customPack') }}<input type="file" accept="application/json,.json" @change="loadPack" /></label>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <label>{{ t('theme.mode') }}<select v-model="settings.mode"><option value="dark">{{ t('theme.dark') }}</option><option value="light">{{ t('theme.light') }}</option></select></label>
      <label>{{ t('theme.primary') }}<input v-model="settings.primary" type="color" /></label>
      <label>{{ t('theme.secondary') }}<input v-model="settings.secondary" type="color" /></label>
      <label>{{ t('theme.background') }}<input v-model="settings.background" type="color" /></label>
      <button type="button" class="action-btn" @click="settings.background = ''; persist()">{{ t('theme.defaultBackground') }}</button>
      <label>{{ t('theme.card') }}<select v-model="settings.card"><option value="raised">{{ t('theme.raised') }}</option><option value="flat">{{ t('theme.flat') }}</option><option value="outlined">{{ t('theme.outlined') }}</option></select></label>
      <label>{{ t('theme.radius') }}<input v-model.number="settings.radius" type="range" min="0" max="24" /></label>
      <label>{{ t('theme.shadow') }}<input v-model.number="settings.shadow" type="range" min="0" max="24" /></label>
      <label>{{ t('theme.density') }}<select v-model="settings.density"><option value="compact">{{ t('theme.compact') }}</option><option value="standard">{{ t('theme.standard') }}</option><option value="comfortable">{{ t('theme.comfortable') }}</option></select></label>
      <label>{{ t('theme.motion') }}<select v-model="settings.motion"><option :value="true">{{ t('theme.on') }}</option><option :value="false">{{ t('theme.off') }}</option></select></label>
      <label>{{ t('theme.tech') }}<select v-model="settings.tech"><option value="none">{{ t('theme.none') }}</option><option value="subtle">{{ t('theme.subtle') }}</option><option value="strong">{{ t('theme.strong') }}</option></select></label>
    </div>
  </div>
</template>
