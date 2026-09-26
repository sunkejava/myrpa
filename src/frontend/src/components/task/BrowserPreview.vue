<script setup lang="ts">
import { onUnmounted, ref, watch } from 'vue'
const props = defineProps<{ token: string; executionId: string; artifactId: string; fileName: string }>()
const url = ref('')
const error = ref('')
const loading = ref(false)
function clear() { if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch(() => [props.executionId, props.artifactId], clear)
onUnmounted(clear)
async function preview() {
  if (url.value) { clear(); return }
  error.value = ''; loading.value = true
  try {
    const response = await fetch(`/api/executions/${props.executionId}/artifacts/${props.artifactId}/content`,
      { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`预览失败 (${response.status})`)
    const blob = await response.blob()
    if (!blob.type.startsWith('image/')) throw new Error('该产物不是图片。')
    url.value = URL.createObjectURL(blob)
  } catch (e) { error.value = e instanceof Error ? e.message : '预览失败' }
  finally { loading.value = false }
}
</script>

<template>
  <div class="browser-preview"><button class="action-btn" type="button" :disabled="loading" @click="preview">{{ url ? '关闭预览' : '预览' }}</button>
    <p v-if="error" role="alert" class="error">{{ error }}</p><img v-if="url" :src="url" :alt="fileName" /></div>
</template>
