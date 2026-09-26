<script setup lang="ts">
import { ref } from 'vue'
import { useLocale } from '../../locales'

type SearchField = { key: string; label: string; placeholder?: string; type?: 'text' | 'select'; options?: Array<{ label: string; value: string }> }
const props = withDefaults(defineProps<{ fields: SearchField[]; modelValue: Record<string, string>; loading?: boolean; initiallyVisible?: number }>(), { initiallyVisible: 3 })
const emit = defineEmits<{ 'update:modelValue': [values: Record<string, string>]; search: [values: Record<string, string>]; reset: [values: Record<string, string>] }>()
const expanded = ref(false)
const { t } = useLocale()
function update(key: string, value: string) { emit('update:modelValue', { ...props.modelValue, [key]: value }) }
function reset() {
  const values = Object.fromEntries(props.fields.map(field => [field.key, '']))
  emit('update:modelValue', values)
  emit('reset', values)
  emit('search', values)
}
</script>

<template>
  <form class="search-form" @submit.prevent="emit('search', modelValue)">
    <div class="resource-grid">
      <label v-for="(field, index) in fields" v-show="expanded || index < initiallyVisible" :key="field.key">{{ field.label }}
        <select v-if="field.type === 'select'" :value="modelValue[field.key] || ''" @change="update(field.key, ($event.target as HTMLSelectElement).value)">
          <option value="">{{ t('common.all') }}</option><option v-for="option in field.options" :key="option.value" :value="option.value">{{ option.label }}</option>
        </select>
        <input v-else :value="modelValue[field.key] || ''" :placeholder="field.placeholder" @input="update(field.key, ($event.target as HTMLInputElement).value)" />
      </label>
    </div>
    <div class="actions"><button class="action-btn primary" type="submit" :disabled="loading">{{ t('common.query') }}</button><button class="action-btn" type="button" :disabled="loading" @click="reset">{{ t('common.reset') }}</button><button v-if="fields.length > initiallyVisible" class="action-btn" type="button" @click="expanded = !expanded">{{ t(expanded ? 'common.collapse' : 'common.expand') }}</button></div>
  </form>
</template>
