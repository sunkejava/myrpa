<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useLocale } from '../../locales'
import { resourceRequest, type City, type Region, type BusinessSystem as System, type BusinessFunction } from '../../api/modules/resources'
import DataTable from '../table/DataTable.vue'
import FormDialog from '../common/FormDialog.vue'

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
type RegionRow = Region & { kind: string; scope: 'countries' | 'provinces' }
const regionRows = computed<RegionRow[]>(() => [
  ...countries.value.map(item => ({ ...item, kind: t('resources.country'), scope: 'countries' as const })),
  ...provinces.value.map(item => ({ ...item, kind: t('resources.province'), scope: 'provinces' as const }))
])
const regionColumns = computed(() => [{ key: 'name', label: t('resources.region'), sortable: true, filterable: true },
  { key: 'kind', label: t('resources.type'), sortable: true },
  { key: 'enabled', label: t('resources.status'), format: (value: unknown) => t(value ? 'resources.enabled' : 'resources.disabled') }])

const request = <T,>(path: string, body?: object) => resourceRequest<T>(props.token, path, body)
async function loadCities() {
  try { cities.value = await request<City[]>('cities') }
  catch (e) { error.value = e instanceof Error ? e.message : t('resources.cityFailed') }
}
async function refreshRegions() {
  error.value = ''
  try {
    const [nextCountries, nextProvinces, nextCities] = await Promise.all([
      request<Region[]>('countries'),
      countryId.value ? request<Region[]>(`countries/${countryId.value}/provinces`) : Promise.resolve([]),
      request<City[]>('cities')
    ])
    countries.value = nextCountries
    provinces.value = nextProvinces
    cities.value = nextCities
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
    if (cityId.value) await chooseCity(cityId.value)
  } catch (e) { error.value = e instanceof Error ? e.message : t('resources.updateFailed') }
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
    <div v-if="cityId" class="table-wrap"><h3>{{ t('resources.district') }}</h3><table><thead><tr><th>{{ t('resources.code') }}</th><th>{{ t('resources.name') }}</th><th>{{ t('resources.status') }}</th><th v-if="admin">{{ t('resources.actions') }}</th></tr></thead><tbody><tr v-for="item in districts" :key="item.id"><td>{{ item.code }}</td><td>{{ item.name }}</td><td>{{ item.enabled ? t('resources.enabled') : t('resources.disabled') }}</td><td v-if="admin"><button type="button" :disabled="busy" @click="setEnabled(`districts/${item.id}/enabled`, !item.enabled)">{{ item.enabled ? t('resources.disabled') : t('resources.enabled') }}</button></td></tr></tbody></table><p v-if="!districts.length" class="muted empty">{{ t('resources.emptyDistricts') }}</p></div>
    <div v-if="systemId" class="table-wrap"><table><thead><tr><th>{{ t('resources.functionCode') }}</th><th>{{ t('resources.functionName') }}</th></tr></thead><tbody><tr v-for="item in functions" :key="item.id"><td>{{ item.code }}</td><td>{{ item.name }}</td></tr></tbody></table><p v-if="!functions.length" class="muted empty">{{ t('resources.emptyFunctions') }}</p></div>
  </section>
  <section v-if="admin" class="panel"><div class="panel-title"><h3>{{ t('resources.regionStatus') }}</h3></div><DataTable :rows="regionRows" :columns="regionColumns" :loading="busy" :empty-label="t('common.empty')" filename="regions.csv" @refresh="refreshRegions"><template #actions="{ row }"><button type="button" class="action-btn" :disabled="busy" @click="setEnabled(`${(row as RegionRow).scope}/${(row as RegionRow).id}/enabled`, !(row as RegionRow).enabled)">{{ (row as RegionRow).enabled ? t('resources.disabled') : t('resources.enabled') }}</button></template></DataTable></section>
  <FormDialog :open="creating" :title="t('resources.createResource')" :submit-label="t('resources.create')" :busy="busy" @close="creating = false" @submit="create">
      <label>{{ t('resources.resourceType') }}<select v-model="target"><option value="country">{{ t('resources.country') }}</option><option value="province">{{ t('resources.province') }}</option><option value="city">{{ t('resources.city') }}</option><option value="district">{{ t('resources.district') }}</option><option value="system">{{ t('resources.businessSystem') }}</option><option value="function">{{ t('resources.businessFunction') }}</option></select></label>
      <label>{{ t('resources.code') }}<input v-model="newCode" required maxlength="64" /></label>
      <label>{{ t('resources.name') }}<input v-model="newName" required maxlength="128" /></label>
    </FormDialog>
</template>
