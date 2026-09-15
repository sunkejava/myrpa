<script setup lang="ts">
defineProps<{ label: string; tone?: 'success' | 'warning' | 'danger' | 'info' }>()
</script>

<template>
  <span class="status-badge" :data-tone="tone ?? 'info'">{{ label }}</span>
</template>
