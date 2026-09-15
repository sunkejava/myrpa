<script setup lang="ts">
import StatusBadge from '../common/StatusBadge.vue'

type NodeRow = { name: string; kind: string; os: string; pool: string; slots: string; status: string }

defineProps<{ nodes: NodeRow[] }>()
</script>

<template>
  <section class="panel">
    <div class="panel-title"><span>执行节点</span><small>Server → NodeAgent → Worker</small></div>
    <div class="table-wrap">
      <table>
        <thead><tr><th>节点</th><th>类型</th><th>系统</th><th>节点池</th><th>Worker</th><th>状态</th></tr></thead>
        <tbody>
          <tr v-for="node in nodes" :key="node.name">
            <td>{{ node.name }}</td><td>{{ node.kind }}</td><td>{{ node.os }}</td><td>{{ node.pool }}</td><td>{{ node.slots }}</td>
            <td><StatusBadge :label="node.status" :tone="node.status === 'Online' ? 'success' : 'warning'" /></td>
          </tr>
        </tbody>
      </table>
    </div>
  </section>
</template>
