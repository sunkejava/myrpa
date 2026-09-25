<script setup lang="ts">
import { onMounted, ref } from 'vue'

type City = { id: string; code: string; name: string; enabled: boolean }
type System = { id: string; cityId: string; code: string; name: string; enabled: boolean }
type BusinessFunction = { id: string; systemId: string; code: string; name: string }
const props = defineProps<{ token: string; admin: boolean }>()
const cities = ref<City[]>([])
const systems = ref<System[]>([])
const functions = ref<BusinessFunction[]>([])
const cityId = ref('')
const systemId = ref('')
const newCode = ref('')
const newName = ref('')
const error = ref('')
const busy = ref(false)

async function request<T>(path: string, body?: object): Promise<T> {
  const response = await fetch(`/api/business-resources/${path}`, {
    method: body ? 'POST' : 'GET',
    headers: { Authorization: `Bearer ${props.token}`, 'Content-Type': 'application/json' },
    ...(body ? { body: JSON.stringify(body) } : {})
  })
  if (!response.ok) throw new Error(`请求失败 (${response.status})`)
  return await response.json() as T
}
async function loadCities() {
  try { cities.value = await request<City[]>('cities') }
  catch (e) { error.value = e instanceof Error ? e.message : '加载城市失败' }
}
async function chooseCity(id: string) {
  cityId.value = id
  systemId.value = ''
  functions.value = []
  try { systems.value = id ? await request<System[]>(`cities/${id}/systems`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '加载系统失败' }
}
async function chooseSystem(id: string) {
  systemId.value = id
  try { functions.value = id ? await request<BusinessFunction[]>(`systems/${id}/functions`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '加载功能失败' }
}
async function create() {
  error.value = ''
  busy.value = true
  try {
    const code = newCode.value.trim(), name = newName.value.trim()
    if (systemId.value) {
      await request(`systems/${systemId.value}/functions`, { code, name })
      await chooseSystem(systemId.value)
    } else if (cityId.value) {
      await request(`cities/${cityId.value}/systems`, { code, name })
      await chooseCity(cityId.value)
    } else {
      await request('cities', { code, name })
      await loadCities()
    }
    newCode.value = ''
    newName.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '创建失败' }
  finally { busy.value = false }
}
onMounted(loadCities)
</script>

<template>
  <section class="panel form-panel resource-panel">
    <h2>城市与业务资源</h2>
    <p class="muted">选择城市和系统，查看各自的业务功能。系统编码可在不同城市重复。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <div class="resource-grid">
      <label>城市<select :value="cityId" @change="chooseCity(($event.target as HTMLSelectElement).value)"><option value="">选择城市</option><option v-for="city in cities" :key="city.id" :value="city.id">{{ city.name }} ({{ city.code }}){{ city.enabled ? '' : ' · 已停用' }}</option></select></label>
      <label>系统<select :value="systemId" :disabled="!cityId" @change="chooseSystem(($event.target as HTMLSelectElement).value)"><option value="">选择系统</option><option v-for="system in systems" :key="system.id" :value="system.id">{{ system.name }} ({{ system.code }}){{ system.enabled ? '' : ' · 已停用' }}</option></select></label>
    </div>
    <div v-if="systemId" class="table-wrap"><table><thead><tr><th>功能编码</th><th>功能名称</th></tr></thead><tbody><tr v-for="item in functions" :key="item.id"><td>{{ item.code }}</td><td>{{ item.name }}</td></tr></tbody></table><p v-if="!functions.length" class="muted empty">该系统尚无业务功能。</p></div>
    <form v-if="admin" @submit.prevent="create">
      <h3>{{ systemId ? '新增功能' : cityId ? '新增业务系统' : '新增城市' }}</h3>
      <label>编码<input v-model="newCode" required maxlength="64" /></label>
      <label>名称<input v-model="newName" required maxlength="128" /></label>
      <button class="action-btn primary" :disabled="busy">创建</button>
    </form>
  </section>
</template>
