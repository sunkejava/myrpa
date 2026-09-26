<script setup lang="ts">
import { computed, ref } from 'vue'
import WorkflowStepEditor from './WorkflowStepEditor.vue'
import type { Step } from './WorkflowStepEditor.vue'

const props = defineProps<{ title: string; steps?: unknown; depth: number }>()
const emit = defineEmits<{ update: [steps: Step[]] }>()
const types = ['Navigate', 'Click', 'Input', 'Select', 'Wait', 'WaitForElement', 'Extract', 'Assert', 'Download', 'Upload', 'Screenshot', 'Condition', 'Loop', 'SubWorkflow', 'HumanTask', 'UKeySign', 'End']
const selectedType = ref('WaitForElement')
const items = computed<Step[]>(() => Array.isArray(props.steps) ? props.steps as Step[] : [])

function add() {
  if (items.value.length >= 1000) return
  const type = selectedType.value
  const config = type === 'Loop' || type === 'SubWorkflow' ? { steps: [] } : type === 'Condition' ? { then: [], else: [] } : {}
  emit('update', [...items.value, { id: `step-${crypto.randomUUID().slice(0, 8)}`, type, config }])
}
function edit(index: number, step: Step) {
  const next = [...items.value]
  next[index] = step
  emit('update', next)
}
function remove(index: number) { emit('update', items.value.filter((_, i) => i !== index)) }
function move(from: number, to: number) {
  if (to < 0 || to >= items.value.length) return
  const next = [...items.value]
  next.splice(to, 0, ...next.splice(from, 1))
  emit('update', next)
}
</script>

<template>
  <section class="nested-workflow-steps" :aria-label="title">
    <h4>{{ title }} · {{ items.length }} 个步骤</h4>
    <WorkflowStepEditor v-for="(item, index) in items" :key="`${item.id || item.type}-${index}`"
      :step="item" :index="index" :total="items.length" :depth="depth"
      @update="edit" @remove="remove" @move="move" />
    <div class="nested-actions">
      <label>新增步骤类型 <select v-model="selectedType"><option v-for="type in types" :key="type" :value="type">{{ type }}</option></select></label>
      <button class="action-btn" type="button" :disabled="items.length >= 1000" @click="add">添加到{{ title }}</button>
    </div>
  </section>
</template>

<style scoped>
.nested-workflow-steps{margin:12px 0;padding:12px;border:1px solid var(--border-color,#d4dce7);border-radius:8px}
.nested-workflow-steps h4{margin:0 0 8px}
.nested-actions{display:flex;align-items:end;gap:10px;flex-wrap:wrap;margin-top:10px}
</style>
