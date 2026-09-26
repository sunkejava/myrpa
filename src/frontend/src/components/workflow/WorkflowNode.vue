<script setup lang="ts">
defineProps<{ stepId: string; type: string; index: number; selected?: boolean }>()
defineEmits<{ select: [id: string] }>()
</script>

<template>
  <button type="button" class="workflow-node" :class="{ selected }" :aria-pressed="selected" @click="$emit('select', stepId)">
    <span class="eyebrow">STEP {{ index + 1 }}</span><strong>{{ type }}</strong><small>{{ stepId }}</small>
  </button>
</template>
