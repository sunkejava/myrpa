import { ref } from 'vue'
import * as common from './common'
import * as system from './system'
import * as task from './task'
import * as workflow from './workflow'
import * as permission from './permission'
import * as audit from './audit'
import * as usage from './usage'
import * as theme from './theme'

const catalogs = { zh: { common: common.zh, system: system.zh, task: task.zh, workflow: workflow.zh, permission: permission.zh, audit: audit.zh, usage: usage.zh, theme: theme.zh },
  en: { common: common.en, system: system.en, task: task.en, workflow: workflow.en, permission: permission.en, audit: audit.en, usage: usage.en, theme: theme.en } }
type Language = 'zh' | 'en' | 'custom'
type Pack = Record<string, Record<string, string>>
const savedLanguage = localStorage.getItem('agentrpa-language')
const language = ref<Language>(savedLanguage === 'en' || savedLanguage === 'custom' ? savedLanguage : 'zh')
function readCustom(): Pack {
  try { return JSON.parse(localStorage.getItem('agentrpa-language-pack') || '{}') as Pack }
  catch { return {} }
}
const custom = ref<Pack>(readCustom())
function t(key: string): string {
  const [module, field] = key.split('.')
  if (!module || !field) return key
  const source: Pack = language.value === 'en' ? catalogs.en : catalogs.zh
  return (language.value === 'custom' ? custom.value[module]?.[field] : undefined) ?? source[module]?.[field] ?? (catalogs.zh as Pack)[module]?.[field] ?? key
}
function setLanguage(next: Language) { language.value = next; localStorage.setItem('agentrpa-language', next) }
async function importLanguagePack(file: File) {
  if (file.size > 100_000) throw new Error('语言包不能超过 100 KB。')
  const parsed: unknown = JSON.parse(await file.text())
  if (!parsed || typeof parsed !== 'object' || Array.isArray(parsed)) throw new Error('语言包格式错误。')
  const pack: Pack = {}
  for (const [module, entries] of Object.entries(parsed)) {
    if (!entries || typeof entries !== 'object' || Array.isArray(entries)) throw new Error('语言包模块格式错误。')
    pack[module] = {}
    for (const [field, value] of Object.entries(entries)) {
      if (typeof value !== 'string') throw new Error('语言资源必须是文本。')
      pack[module][field] = value
    }
  }
  custom.value = pack
  localStorage.setItem('agentrpa-language-pack', JSON.stringify(pack))
  setLanguage('custom')
}
export function useLocale() { return { language, t, setLanguage, importLanguagePack } }
