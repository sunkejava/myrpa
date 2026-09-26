<script setup lang="ts">
import { nextTick, ref, useId, watch } from 'vue'
import { useLocale } from '../../locales'
const props = defineProps<{ open: boolean; title: string }>()
const emit = defineEmits<{ close: [] }>()
const id = useId()
const { t } = useLocale()
const panel = ref<HTMLElement | null>(null)
watch(() => props.open, value => { if (value) nextTick(() => panel.value?.focus()) })
</script>

<template>
  <div v-if="open" class="dialog-backdrop" @click.self="emit('close')">
    <aside ref="panel" role="dialog" aria-modal="true" :aria-labelledby="id" class="detail-drawer" tabindex="-1" @keydown.esc="emit('close')">
      <div class="panel-title"><h2 :id="id">{{ title }}</h2><button class="action-btn" @click="emit('close')">{{ t('common.close') }}</button></div>
      <slot />
    </aside>
  </div>
</template>
