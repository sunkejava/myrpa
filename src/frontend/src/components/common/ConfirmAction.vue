<script setup lang="ts">
import { ref } from 'vue'
import FormDialog from './FormDialog.vue'
defineProps<{ label: string; title: string; message: string; disabled?: boolean; busy?: boolean }>()
const emit = defineEmits<{ confirmed: [] }>()
const open = ref(false)
function confirm() { open.value = false; emit('confirmed') }
</script>

<template>
  <button type="button" class="action-btn" :disabled="disabled || busy" @click="open = true">{{ label }}</button>
  <FormDialog :open="open" :title="title" :busy="busy" :submit-label="label" @close="open = false" @submit="confirm"><p class="muted">{{ message }}</p></FormDialog>
</template>
