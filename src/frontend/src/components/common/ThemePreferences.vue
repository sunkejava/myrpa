<script setup lang="ts">
import { ref } from 'vue'
import { useTheme } from '../../composables/useTheme'
import { useLocale } from '../../locales'
const { settings, persist } = useTheme()
const { language, t, setLanguage, importLanguagePack } = useLocale()
const error = ref('')
async function loadPack(event: Event) {
  const file = (event.target as HTMLInputElement).files?.[0]
  if (!file) return
  error.value = ''
  try { await importLanguagePack(file) }
  catch (reason) { error.value = reason instanceof Error ? reason.message : '语言包导入失败' }
}
</script>

<template>
  <details class="theme-settings">
    <summary class="action-btn">{{ t('common.theme') }}</summary>
    <div class="theme-settings-panel" @change="persist">
      <label>{{ t('common.language') }}<select :value="language" @change="setLanguage(($event.target as HTMLSelectElement).value as 'zh' | 'en' | 'custom')"><option value="zh">简体中文</option><option value="en">English</option><option value="custom">Custom</option></select></label>
      <label>{{ t('common.customPack') }}<input type="file" accept="application/json,.json" @change="loadPack" /></label>
      <p v-if="error" class="error" role="alert">{{ error }}</p>
      <label>模式 / Mode<select v-model="settings.mode"><option value="dark">Dark</option><option value="light">Light</option></select></label>
      <label>主色 / Primary<input v-model="settings.primary" type="color" /></label>
      <label>辅助色 / Secondary<input v-model="settings.secondary" type="color" /></label>
      <label>背景 / Background<input v-model="settings.background" type="color" /></label>
      <button type="button" class="action-btn" @click="settings.background = ''; persist()">默认背景 / Default background</button>
      <label>卡片 / Cards<select v-model="settings.card"><option value="raised">Raised</option><option value="flat">Flat</option><option value="outlined">Outlined</option></select></label>
      <label>圆角 / Radius<input v-model.number="settings.radius" type="range" min="0" max="24" /></label>
      <label>阴影 / Shadow<input v-model.number="settings.shadow" type="range" min="0" max="24" /></label>
      <label>密度 / Density<select v-model="settings.density"><option value="compact">Compact</option><option value="standard">Standard</option><option value="comfortable">Comfortable</option></select></label>
      <label>动效 / Motion<select v-model="settings.motion"><option :value="true">On</option><option :value="false">Off</option></select></label>
      <label>科技元素 / Tech accents<select v-model="settings.tech"><option value="none">None</option><option value="subtle">Subtle</option><option value="strong">Strong</option></select></label>
    </div>
  </details>
</template>
