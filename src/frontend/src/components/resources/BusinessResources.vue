<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLocale } from '../../locales'
import { resourceRequest, type City, type Region, type BusinessSystem as System, type BusinessFunction } from '../../api/modules/resources'
import DataTable from '../table/DataTable.vue'
import FormDialog from '../common/FormDialog.vue'
import { apiRequest } from '../../api/http'

const props = defineProps<{ token: string; admin: boolean }>()
const { t } = useLocale()
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
const creating = ref(false)
const editingSystem = ref<System | null>(null)
const systemBaseUrl = ref('')
type RegionRow = Region & { kind: string; scope: 'countries' | 'provinces' | 'cities' }
const regionRows = computed<RegionRow[]>(() => [
  ...countries.value.map(item => ({ ...item, kind: t('resources.country'), scope: 'countries' as const })),
  ...provinces.value.map(item => ({ ...item, kind: t('resources.province'), scope: 'provinces' as const })),
  ...cities.value.map(item => ({ ...item, kind: t('resources.city'), scope: 'cities' as const }))
])
const regionColumns = computed(() => [{ key: 'name', label: t('resources.region'), sortable: true, filterable: true },
  { key: 'kind', label: t('resources.type'), sortable: true },
  { key: 'enabled', label: t('resources.status'), format: (value: unknown) => t(value ? 'resources.enabled' : 'resources.disabled') }])
const itemColumns = computed(() => [{ key: 'code', label: t('resources.code'), sortable: true, filterable: true },
  { key: 'name', label: t('resources.name'), sortable: true, filterable: true },
  { key: 'enabled', label: t('resources.status'), format: (value: unknown) => t(value ? 'resources.enabled' : 'resources.disabled') }])
const systemColumns = computed(() => [...itemColumns.value, { key: 'baseUrl', label: '系统地址', format: (value: unknown) => value ? String(value) : '待配置' }])
const functionColumns = computed(() => [{ key: 'code', label: t('resources.functionCode'), sortable: true, filterable: true },
  { key: 'name', label: t('resources.functionName'), sortable: true, filterable: true }])

const request = <T,>(path: string, body?: object) => resourceRequest<T>(props.token, path, body)
async function loadCities() {
  try { cities.value = await request<City[]>('cities') }
  catch (e) { error.value = e instanceof Error ? e.message : t('resources.cityFailed') }
}
async function refreshRegions() {
  error.value = ''
  try {
    const [nextCountries, nextCities] = await Promise.all([request<Region[]>('countries'), request<City[]>('cities')])
    countries.value = nextCountries
    cities.value = nextCities
    if (!countryId.value) countryId.value = nextCountries.find(x => x.code === 'CN')?.id || nextCountries[0]?.id || ''
    provinces.value = countryId.value ? await request<Region[]>(`countries/${countryId.value}/provinces`) : []
    if (!provinceId.value) provinceId.value = provinces.value.find(x => x.code === 'BJ')?.id || provinces.value[0]?.id || ''
    if (!cityId.value) {
      const city = nextCities.find(x => x.provinceId === provinceId.value && x.code === 'CN-BJ') ||
        nextCities.find(x => x.provinceId === provinceId.value)
      if (city) await chooseCity(city.id)
    }
  } catch (e) { error.value = e instanceof Error ? e.message : t('resources.updateFailed') }
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
  catch (e) { error.value = e instanceof Error ? e.message : t('resources.provinceFailed') }
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
  catch (e) { error.value = e instanceof Error ? e.message : t('resources.systemFailed') }
}
async function chooseSystem(id: string) {
  systemId.value = id
  try { functions.value = id ? await request<BusinessFunction[]>(`systems/${id}/functions`) : [] }
  catch (e) { error.value = e instanceof Error ? e.message : t('resources.functionFailed') }
}
async function setEnabled(path: string, enabled: boolean) {
  error.value = ''
  busy.value = true
  try {
    await request(path, { enabled })
    countries.value = await request<Region[]>('countries')
    await loadCities()
    if (countryId.value) provinces.value = await request<Region[]>(`countries/${countryId.value}/provinces`)
    if (cityId.value) {
      const previousSystemId = systemId.value
      await chooseCity(cityId.value)
      if (previousSystemId && systems.value.some(item => item.id === previousSystemId)) await chooseSystem(previousSystemId)
    }
  } catch (e) { error.value = e instanceof Error ? e.message : t('resources.updateFailed') }
  finally { busy.value = false }
}
function editSystem(system: System) { editingSystem.value = system; systemBaseUrl.value = system.baseUrl || '' }
async function saveSystemBaseUrl() {
  if (!editingSystem.value) return
  busy.value = true; error.value = ''
  try {
    await apiRequest(`business-resources/systems/${editingSystem.value.id}/base-url`, props.token, {
      method: 'PUT', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ baseUrl: systemBaseUrl.value.trim() })
    })
    editingSystem.value = null
    await chooseCity(cityId.value)
  } catch (e) { error.value = e instanceof Error ? e.message : '保存系统地址失败' }
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
      if (!countryId.value) throw new Error(t('resources.chooseCountryFirst'))
      await request(`countries/${countryId.value}/provinces`, { code, name })
      await chooseCountry(countryId.value)
    } else if (target.value === 'district') {
      if (!cityId.value) throw new Error(t('resources.chooseCityFirst'))
      await request(`cities/${cityId.value}/districts`, { code, name })
      await chooseCity(cityId.value)
    } else if (target.value === 'function') {
      if (!systemId.value) throw new Error(t('resources.chooseSystemFirst'))
      await request(`systems/${systemId.value}/functions`, { code, name })
      await chooseSystem(systemId.value)
    } else if (target.value === 'system') {
      if (!cityId.value) throw new Error(t('resources.chooseCityFirst'))
      await request(`cities/${cityId.value}/systems`, { code, name })
      await chooseCity(cityId.value)
    } else {
      await request('cities', { code, name, provinceId: provinceId.value || null })
      await loadCities()
    }
    newCode.value = ''
    newName.value = ''
    creating.value = false
  } catch (e) { error.value = e instanceof Error ? e.message : t('resources.createFailed') }
  finally { busy.value = false }
}
onMounted(refreshRegions)
</script>

<template>
  <div class="page-toolbar"><div><h2>{{ t('resources.title') }}</h2><p class="muted">{{ t('resources.help') }}</p></div><button v-if="admin" class="action-btn primary" @click="creating = true">{{ t('resources.createResource') }}</button></div>
  <section class="panel form-panel resource-panel">
    <p v-if="error" class="error" role="alert">{{ error }}</p>
    <div class="resource-grid">
      <label>{{ t('resources.country') }}<select :value="countryId" @change="chooseCountry(($event.target as HTMLSelectElement).value)"><option value="">{{ t('resources.chooseCountry') }}</option><option v-for="item in countries" :key="item.id" :value="item.id">{{ item.name }} ({{ item.code }}){{ item.enabled ? '' : t('resources.disabledSuffix') }}</option></select></label>
      <label>{{ t('resources.province') }}<select :value="provinceId" :disabled="!countryId" @change="chooseProvince(($event.target as HTMLSelectElement).value)"><option value="">{{ t('resources.chooseProvince') }}</option><option v-for="item in provinces" :key="item.id" :value="item.id">{{ item.name }} ({{ item.code }}){{ item.enabled ? '' : t('resources.disabledSuffix') }}</option></select></label>
      <label>{{ t('resources.city') }}<select :value="cityId" @change="chooseCity(($event.target as HTMLSelectElement).value)"><option value="">{{ t('resources.chooseCity') }}</option><option v-for="city in cities.filter(x => !provinceId || x.provinceId === provinceId)" :key="city.id" :value="city.id">{{ city.name }} ({{ city.code }}){{ city.enabled ? '' : t('resources.disabledSuffix') }}</option></select></label>
      <label>{{ t('resources.system') }}<select :value="systemId" :disabled="!cityId" @change="chooseSystem(($event.target as HTMLSelectElement).value)"><option value="">{{ t('resources.chooseSystem') }}</option><option v-for="system in systems" :key="system.id" :value="system.id">{{ system.name }} ({{ system.code }}){{ system.enabled ? '' : t('resources.disabledSuffix') }}</option></select></label>
    </div>
  </section>
  <section v-if="cityId" class="panel"><div class="panel-title"><h3>{{ t('resources.businessSystem') }}</h3></div><DataTable :rows="systems" :columns="systemColumns" :empty-label="t('resources.emptySystems')" filename="business-systems.csv" @refresh="chooseCity(cityId)"><template #actions="{ row }"><button v-if="admin" type="button" class="action-btn" :disabled="busy" @click="editSystem(row as System)">配置地址</button><button v-if="admin" type="button" class="action-btn" :disabled="busy" @click="setEnabled(`systems/${(row as System).id}/enabled`, !(row as System).enabled)">{{ (row as System).enabled ? t('resources.disabled') : t('resources.enabled') }}</button></template></DataTable></section>
  <section v-if="cityId" class="panel"><div class="panel-title"><h3>{{ t('resources.district') }}</h3></div><DataTable :rows="districts" :columns="itemColumns" :empty-label="t('resources.emptyDistricts')" filename="districts.csv" @refresh="chooseCity(cityId)"><template #actions="{ row }"><button v-if="admin" type="button" class="action-btn" :disabled="busy" @click="setEnabled(`districts/${(row as Region).id}/enabled`, !(row as Region).enabled)">{{ (row as Region).enabled ? t('resources.disabled') : t('resources.enabled') }}</button></template></DataTable></section>
  <section v-if="systemId" class="panel"><div class="panel-title"><h3>{{ t('resources.businessFunction') }}</h3></div><DataTable :rows="functions" :columns="functionColumns" :empty-label="t('resources.emptyFunctions')" filename="business-functions.csv" @refresh="chooseSystem(systemId)" /></section>
  <section v-if="admin" class="panel"><div class="panel-title"><h3>{{ t('resources.regionStatus') }}</h3></div><DataTable :rows="regionRows" :columns="regionColumns" :loading="busy" :empty-label="t('common.empty')" filename="regions.csv" @refresh="refreshRegions"><template #actions="{ row }"><button type="button" class="action-btn" :disabled="busy" @click="setEnabled(`${(row as RegionRow).scope}/${(row as RegionRow).id}/enabled`, !(row as RegionRow).enabled)">{{ (row as RegionRow).enabled ? t('resources.disabled') : t('resources.enabled') }}</button></template></DataTable></section>
  <FormDialog :open="creating" :title="t('resources.createResource')" :submit-label="t('resources.create')" :busy="busy" @close="creating = false" @submit="create">
      <label>{{ t('resources.resourceType') }}<select v-model="target"><option value="country">{{ t('resources.country') }}</option><option value="province">{{ t('resources.province') }}</option><option value="city">{{ t('resources.city') }}</option><option value="district">{{ t('resources.district') }}</option><option value="system">{{ t('resources.businessSystem') }}</option><option value="function">{{ t('resources.businessFunction') }}</option></select></label>
      <label>{{ t('resources.code') }}<input v-model="newCode" required maxlength="64" /></label>
      <label>{{ t('resources.name') }}<input v-model="newName" required maxlength="128" /></label>
    </FormDialog>
  <FormDialog :open="!!editingSystem" title="配置业务系统地址" submit-label="保存地址" :busy="busy" @close="editingSystem = null" @submit="saveSystemBaseUrl">
    <label>系统地址（HTTP/HTTPS）<input v-model.trim="systemBaseUrl" type="url" maxlength="1024" placeholder="https://经授权的业务系统地址" /></label>
    <p class="muted">工作流 Navigate 步骤可以使用 <code v-text="'{{systemBaseUrl}}'"></code>。地址为空时引用该变量的任务会等待资源配置。</p>
  </FormDialog>
</template>
