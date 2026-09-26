<script setup lang="ts">
import { computed, ref } from 'vue'
import WorkflowNode from './WorkflowNode.vue'

type Step = { id?: string; type?: string; config?: Record<string, unknown> }
const props = defineProps<{ steps: Step[] }>()
const selectedId = ref('')
const current = computed(() => props.steps.find(step => step.id === selectedId.value))
</script>

<template>
  <section class="workflow-canvas" aria-label="Workflow 步骤预览">
    <div v-if="steps.length" class="workflow-track"><WorkflowNode v-for="(step, index) in steps" :key="`${step.id || 'step'}-${index}`" :step-id="step.id || `#${index + 1}`" :type="step.type || 'Unknown'" :index="index" :selected="selectedId === (step.id || `#${index + 1}`)" @select="selectedId = $event" /></div>
    <p v-else class="muted empty">暂无 Workflow 步骤。</p>
    <div v-if="current" class="workflow-step-detail"><strong>{{ current.id }} · {{ current.type }}</strong><pre>{{ JSON.stringify(current.config || {}, null, 2) }}</pre></div>
  </section>
</template>
