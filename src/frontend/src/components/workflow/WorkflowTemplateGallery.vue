<script setup lang="ts">
import { computed } from 'vue'
import { workflowTemplates, type WorkflowTemplate } from '../../data/workflow-templates/catalog'
import WorkflowCanvas from './WorkflowCanvas.vue'

const emit = defineEmits<{ select: [template: WorkflowTemplate] }>()
const templates = computed(() => workflowTemplates.filter(item => !item.developmentOnly || import.meta.env.DEV))
const steps = (template: WorkflowTemplate) => (template.definition.steps as { id?: string; type?: string; config?: Record<string, unknown> }[]) || []
</script>

<template>
  <section class="panel workflow-gallery">
    <div class="panel-title"><h3>默认工作流示例</h3></div>
    <p class="muted">示例不会自动发布或执行。选中后先绑定正确的城市、业务系统与功能，核对页面定位和权限，再发布新版本。</p>
    <div class="workflow-gallery-grid">
      <article v-for="item in templates" :key="item.code" class="workflow-gallery-card">
        <h4>{{ item.name }}</h4>
        <p>{{ item.summary }}</p>
        <p class="muted">依赖资源：{{ item.resource }}</p>
        <details><summary>预览 {{ steps(item).length }} 个步骤与前置条件</summary>
          <p class="muted">{{ item.requirements }}</p>
          <WorkflowCanvas :steps="steps(item)" />
        </details>
        <button type="button" class="action-btn" @click="emit('select', item)">载入为草稿</button>
      </article>
    </div>
  </section>
</template>

<style scoped>
.workflow-gallery-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(min(100%,280px),1fr));gap:12px}
.workflow-gallery-card{border:1px solid var(--border-color, #d4dce7);border-radius:12px;padding:16px;min-width:0}
.workflow-gallery-card h4{margin:0 0 8px}
.workflow-gallery-card p{margin:0 0 10px}
.workflow-gallery-card details{margin-bottom:12px}
</style>
