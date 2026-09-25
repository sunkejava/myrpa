<script setup lang="ts">
import { onMounted, ref } from 'vue'

type City = { id: string; code: string; name: string; enabled: boolean; provinceId?: string | null }
type Region = { id: string; code: string; name: string; enabled: boolean }
type System = { id: string; cityId: string; code: string; name: string; enabled: boolean }
type BusinessFunction = { id: string; systemId: string; code: string; name: string }
const props = defineProps<{ token: string; admin: boolean }>()
const cities = ref<City[]>([])
const countries = ref<Region[]>([])
const provinces = ref<Region[]>([])
const districts = ref<Region[]>([])
const countryId = ref('')
const provinceId = ref('')
const target = ref<'country' | 'province' | 'city' | 'district' | 'system' | 'function'>('city')
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
async function chooseCountry(id: string) {
  countryId.value = id
  provinceId.value = ''
  cityId.value = ''
  systemId.value = ''
  systems.value = []
  functions.value = []
  districts.value = []
  try { provinces.value = id ? await request<Region[]>(`countries/${id}/provinces`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '加载省份失败' }
}
async function chooseProvince(id: string) {
  provinceId.value = id
  cityId.value = ''
  systemId.value = ''
  systems.value = []
  districts.value = []
  functions.value = []
}
async function chooseCity(id: string) {
  cityId.value = id
  systemId.value = ''
  functions.value = []
  try {
    const [nextSystems, nextDistricts] = id ? await Promise.all([
      request<System[]>(`cities/${id}/systems`), request<Region[]>(`cities/${id}/districts`)
    ]) : [[], []]
    systems.value = nextSystems
    districts.value = nextDistricts
  }
  catch (e) { error.value = e instanceof Error ? e.message : '加载系统失败' }
}
async function chooseSystem(id: string) {
  systemId.value = id
  try { functions.value = id ? await request<BusinessFunction[]>(`systems/${id}/functions`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : '加载功能失败' }
}
async function setEnabled(path: string, enabled: boolean) {
  error.value = ''
  busy.value = true
  try {
    await request(path, { enabled })
    countries.value = await request<Region[]>('countries')
    await loadCities()
    if (countryId.value) provinces.value = await request<Region[]>(`countries/${countryId.value}/provinces`)
    if (cityId.value) await chooseCity(cityId.value)
  } catch (e) { error.value = e instanceof Error ? e.message : '更新资源状态失败' }
  finally { busy.value = false }
}
async function create() {
  error.value = ''
  busy.value = true
  try {
    const code = newCode.value.trim(), name = newName.value.trim()
    if (target.value === 'country') {
      await request('countries', { code, name })
      countries.value = await request<Region[]>('countries')
    } else if (target.value === 'province') {
      if (!countryId.value) throw new Error('请先选择国家')
      await request(`countries/${countryId.value}/provinces`, { code, name })
      await chooseCountry(countryId.value)
    } else if (target.value === 'district') {
      if (!cityId.value) throw new Error('请先选择城市')
      await request(`cities/${cityId.value}/districts`, { code, name })
      await chooseCity(cityId.value)
    } else if (target.value === 'function') {
      if (!systemId.value) throw new Error('请先选择系统')
      await request(`systems/${systemId.value}/functions`, { code, name })
      await chooseSystem(systemId.value)
    } else if (target.value === 'system') {
      if (!cityId.value) throw new Error('请先选择城市')
      await request(`cities/${cityId.value}/systems`, { code, name })
      await chooseCity(cityId.value)
    } else {
      await request('cities', { code, name, provinceId: provinceId.value || null })
      await loadCities()
    }
    newCode.value = ''
    newName.value = ''
  } catch (e) { error.value = e instanceof Error ? e.message : '创建失败' }
  finally { busy.value = false }
}
onMounted(async () => {
  await loadCities()
  try { countries.value = await request<Region[]>('countries') }
  catch (e) { error.value = e instanceof Error ? e.message : '加载国家失败' }
})
</script>

<template>
  <section class="panel form-panel resource-panel">
    <h2>区域与业务资源</h2>
    <p class="muted">国家、省份、城市、区县构成区域树；未归属省份的历史城市继续保留。</p>
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <div class="resource-grid">
      <label>国家<select :value="countryId" @change="chooseCountry(($event.target as HTMLSelectElement).value)"><option value="">选择国家</option><option v-for="item in countries" :key="item.id" :value="item.id">{{ item.name }} ({{ item.code }}){{ item.enabled ? '' : ' · 已停用' }}</option></select></label>
      <label>省份<select :value="provinceId" :disabled="!countryId" @change="chooseProvince(($event.target as HTMLSelectElement).value)"><option value="">选择省份</option><option v-for="item in provinces" :key="item.id" :value="item.id">{{ item.name }} ({{ item.code }}){{ item.enabled ? '' : ' · 已停用' }}</option></select></label>
      <label>城市<select :value="cityId" @change="chooseCity(($event.target as HTMLSelectElement).value)"><option value="">选择城市</option><option v-for="city in cities.filter(x => !provinceId || x.provinceId === provinceId)" :key="city.id" :value="city.id">{{ city.name }} ({{ city.code }}){{ city.enabled ? '' : ' · 已停用' }}</option></select></label>
      <label>系统<select :value="systemId" :disabled="!cityId" @change="chooseSystem(($event.target as HTMLSelectElement).value)"><option value="">选择系统</option><option v-for="system in systems" :key="system.id" :value="system.id">{{ system.name }} ({{ system.code }}){{ system.enabled ? '' : ' · 已停用' }}</option></select></label>
    </div>
    <div v-if="admin" class="table-wrap"><h3>区域状态</h3><table><thead><tr><th>区域</th><th>类型</th><th>状态</th><th>操作</th></tr></thead><tbody><tr v-for="item in countries" :key="item.id"><td>{{ item.name }}</td><td>国家</td><td>{{ item.enabled ? '启用' : '停用' }}</td><td><button type="button" :disabled="busy" @click="setEnabled(`countries/${item.id}/enabled`, !item.enabled)">{{ item.enabled ? '停用' : '启用' }}</button></td></tr><tr v-for="item in provinces" :key="item.id"><td>{{ item.name }}</td><td>省份</td><td>{{ item.enabled ? '启用' : '停用' }}</td><td><button type="button" :disabled="busy" @click="setEnabled(`provinces/${item.id}/enabled`, !item.enabled)">{{ item.enabled ? '停用' : '启用' }}</button></td></tr></tbody></table></div>
    <div v-if="cityId" class="table-wrap"><h3>区县</h3><table><thead><tr><th>编码</th><th>名称</th><th>状态</th><th v-if="admin">操作</th></tr></thead><tbody><tr v-for="item in districts" :key="item.id"><td>{{ item.code }}</td><td>{{ item.name }}</td><td>{{ item.enabled ? '启用' : '停用' }}</td><td v-if="admin"><button type="button" :disabled="busy" @click="setEnabled(`districts/${item.id}/enabled`, !item.enabled)">{{ item.enabled ? '停用' : '启用' }}</button></td></tr></tbody></table><p v-if="!districts.length" class="muted empty">该城市尚无区县。</p></div>
    <div v-if="systemId" class="table-wrap"><table><thead><tr><th>功能编码</th><th>功能名称</th></tr></thead><tbody><tr v-for="item in functions" :key="item.id"><td>{{ item.code }}</td><td>{{ item.name }}</td></tr></tbody></table><p v-if="!functions.length" class="muted empty">该系统尚无业务功能。</p></div>
    <form v-if="admin" @submit.prevent="create">
      <h3>新增资源</h3>
      <label>资源类型<select v-model="target"><option value="country">国家</option><option value="province">省份</option><option value="city">城市</option><option value="district">区县</option><option value="system">业务系统</option><option value="function">业务功能</option></select></label>
      <label>编码<input v-model="newCode" required maxlength="64" /></label>
      <label>名称<input v-model="newName" required maxlength="128" /></label>
      <button class="action-btn primary" :disabled="busy">创建</button>
    </form>
  </section>
</template>
