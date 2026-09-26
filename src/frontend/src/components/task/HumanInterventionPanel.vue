<script setup lang="ts">
import type { Intervention } from '../../types/intervention'
defineProps<{ rows: Intervention[]; busy: boolean }>()
defineEmits<{ complete: [row: Intervention]; cancel: [row: Intervention] }>()
</script>

<template>
  <div class="table-wrap"><table><thead><tr><th>标题 / 执行</th><th>类型</th><th>状态</th><th>到期</th><th>操作</th></tr></thead><tbody>
    <tr v-for="row in rows" :key="row.id"><td>{{ row.title }}<small>{{ row.executionId }}</small></td><td>{{ row.type }}</td><td>{{ row.status }}</td><td>{{ new Date(row.expiresAt).toLocaleString('zh-CN') }}</td>
      <td><button v-if="row.status === 'Opened' || row.status === 'Pending'" class="action-btn" :disabled="busy || row.type === 'QrLogin'" @click="$emit('complete', row)">完成</button>
        <button v-if="row.status === 'Opened' || row.status === 'Pending'" class="action-btn" :disabled="busy" @click="$emit('cancel', row)">取消</button></td></tr>
  </tbody></table><p v-if="rows.length === 0" class="muted empty">暂无人工介入记录。</p></div>
</template>
