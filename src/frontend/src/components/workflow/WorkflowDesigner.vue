<script setup lang="ts">
import { computed, ref } from 'vue'

type Step = { id: string; type: string; config: Record<string, unknown> }
const steps = ref<Step[]>([
  { id: 'step-1', type: 'Navigate', config: { url: 'https://example.com' } },
  { id: 'step-2', type: 'WaitForElement', config: { selector: '#content', timeout: 30000 } }
])
const selected = ref(0)
const stepTypes = ['Navigate', 'Click', 'Input', 'Select', 'Wait', 'WaitForElement', 'Extract', 'Upload', 'Download', 'Screenshot', 'Condition', 'Loop', 'HumanTask', 'Assert', 'End']
const definition = computed(() => JSON.stringify({ version: 1, steps: steps.value }, null, 2))
function add(type: string) { steps.value.push({ id: `step-${steps.value.length + 1}`, type, config: {} }); selected.value = steps.value.length - 1 }
function remove(index: number) { steps.value.splice(index, 1); selected.value = Math.max(0, selected.value - (selected.value >= steps.value.length ? 1 : 0)) }
</script>

<template>
  <section class="workflow-designer">
    <aside class="designer-palette"><div class="panel-title"><b>Steps</b><small>Deterministic</small></div><button v-for="type in stepTypes" :key="type" @click="add(type)">＋ {{ type }}</button></aside>
    <div class="designer-canvas"><div class="canvas-head"><div><span class="eyebrow">WORKFLOW DESIGNER</span><h3>社保人员核验流程</h3></div><button class="publish-btn">发布版本</button></div><div class="step-list"><article v-for="(step, index) in steps" :key="step.id" :class="['workflow-step', { selected: selected === index }]" @click="selected = index"><span class="step-no">{{ index + 1 }}</span><div><b>{{ step.type }}</b><small>{{ step.id }}</small></div><button @click.stop="remove(index)">×</button></article></div></div>
    <aside class="designer-inspector"><div class="panel-title"><b>配置</b><small>Step Inspector</small></div><template v-if="steps[selected]"><label>Step ID<input v-model="steps[selected].id" /></label><label>Type<select v-model="steps[selected].type"><option v-for="type in stepTypes" :key="type">{{ type }}</option></select></label><label>Config JSON<textarea v-model="definition" readonly /></label></template></aside>
  </section>
</template>

<style scoped>
.workflow-designer{display:grid;grid-template-columns:190px 1fr 330px;height:680px;border:1px solid var(--line);border-radius:12px;overflow:hidden;background:var(--surface)}.designer-palette,.designer-inspector{background:var(--surface2);border-right:1px solid var(--line);padding:14px}.designer-inspector{border-right:0;border-left:1px solid var(--line)}.designer-palette button{width:100%;border:0;background:transparent;color:var(--muted);text-align:left;padding:8px;border-radius:7px;cursor:pointer}.designer-palette button:hover{background:var(--surface);color:var(--text)}.designer-canvas{padding:18px;overflow:auto}.canvas-head{display:flex;justify-content:space-between;align-items:center}.canvas-head h3{margin:5px 0 20px}.publish-btn{border:0;border-radius:7px;padding:9px 14px;background:var(--accent);color:#fff;cursor:pointer}.step-list{max-width:720px;margin:auto;display:grid;gap:10px}.workflow-step{display:flex;align-items:center;gap:12px;padding:15px;border:1px solid var(--line);border-radius:9px;background:var(--surface2);cursor:pointer}.workflow-step.selected{border-color:var(--accent)}.step-no{width:26px;height:26px;border-radius:7px;background:var(--surface);display:grid;place-items:center}.workflow-step small,.designer-inspector small{display:block;color:var(--muted);font-size:11px;margin-top:4px}.workflow-step button{margin-left:auto;border:0;background:transparent;color:var(--muted);cursor:pointer;font-size:18px}.panel-title{display:flex;justify-content:space-between;margin-bottom:12px}.designer-inspector label{display:block;font-size:11px;color:var(--muted);margin:15px 0}.designer-inspector input,.designer-inspector select,.designer-inspector textarea{display:block;width:100%;margin-top:6px;background:var(--surface);border:1px solid var(--line);color:var(--text);border-radius:7px;padding:8px;font:inherit}.designer-inspector textarea{height:430px;font-family:ui-monospace,monospace;font-size:11px}.eyebrow{font-size:10px;letter-spacing:.14em;color:var(--accent)}
</style>
