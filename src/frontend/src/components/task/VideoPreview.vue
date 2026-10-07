<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
const props = defineProps<{ token: string; executionId: string; artifactId: string }>()
const url = ref(''); const error = ref(''); const busy = ref(false)
let generation = 0
function clear() { generation++; if (url.value) URL.revokeObjectURL(url.value); url.value = '' }
watch(() => [props.executionId, props.artifactId], clear)
onBeforeUnmount(clear)
async function play() {
  const current = ++generation; busy.value = true; error.value = ''
  try {
    const response = await fetch(`/api/executions/${props.executionId}/artifacts/${props.artifactId}/content`, { headers: { Authorization: `Bearer ${props.token}` } })
    if (!response.ok) throw new Error(`读取视频失败 (${response.status})`)
    const blob = await response.blob()
    if (current !== generation) return
    if (url.value) URL.revokeObjectURL(url.value)
    url.value = URL.createObjectURL(blob)
  } catch (e) { if (current === generation) error.value = e instanceof Error ? e.message : '读取视频失败' }
  finally { if (current === generation) busy.value = false }
}
</script>
<template>
  <button class="action-btn" :disabled="busy" @click="play">{{ busy ? '加载中' : '播放视频' }}</button>
  <p v-if="error" role="alert" class="error">{{ error }}</p>
  <video v-if="url" :src="url" controls preload="metadata" aria-label="浏览器执行录像" style="display:block;width:100%;max-width:640px;margin-top:12px" />
</template>
