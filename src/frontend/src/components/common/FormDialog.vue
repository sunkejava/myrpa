<script setup lang="ts">
import { nextTick, useId, watch, ref } from 'vue'
import { useLocale } from '../../locales'
const props = defineProps<{ open: boolean; title: string; busy?: boolean; submitLabel?: string }>()
const emit = defineEmits<{ close: []; submit: [] }>()
const titleId = useId()
const { t } = useLocale()
const panel = ref<HTMLElement | null>(null)
watch(() => props.open, value => { if (value) nextTick(() => panel.value?.focus()) })
</script>

<template>
  <div v-if="open" class="dialog-backdrop" @click.self="emit('close')">
    <section ref="panel" class="form-dialog panel" role="dialog" aria-modal="true" :aria-labelledby="titleId" tabindex="-1" @keydown.esc="emit('close')">
      <h3 :id="titleId">{{ title }}</h3>
      <form @submit.prevent="emit('submit')"><slot /><div class="actions"><button type="button" class="action-btn" @click="emit('close')">{{ t('common.cancel') }}</button><button type="submit" class="action-btn primary" :disabled="busy">{{ submitLabel || t('common.save') }}</button></div></form>
    </section>
  </div>
</template>
