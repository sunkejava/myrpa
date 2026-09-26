import { computed, reactive } from 'vue'

export type ThemeOptions = {
  mode: 'light' | 'dark'; primary: string; secondary: string; background: string;
  card: 'raised' | 'flat' | 'outlined'; radius: number; shadow: number;
  density: 'compact' | 'standard' | 'comfortable'; motion: boolean; tech: 'none' | 'subtle' | 'strong'
}
const defaults: ThemeOptions = { mode: 'light', primary: '#2563eb', secondary: '#0891b2', background: '',
  card: 'raised', radius: 10, shadow: 4, density: 'standard', motion: true, tech: 'subtle' }
function restored(): ThemeOptions {
  try {
    const stored = JSON.parse(localStorage.getItem('agentrpa-theme-options') || '{}') as Partial<ThemeOptions>
    return { ...defaults, ...stored, mode: stored.mode || (localStorage.getItem('agentrpa-theme') === 'dark' ? 'dark' : 'light') }
  } catch { return { ...defaults } }
}
const settings = reactive<ThemeOptions>(restored())
function persist() {
  localStorage.setItem('agentrpa-theme-options', JSON.stringify(settings))
  localStorage.setItem('agentrpa-theme', settings.mode)
}
const style = computed(() => ({
  '--accent': settings.primary,
  '--accent-secondary': settings.secondary,
  '--radius': `${Math.min(24, Math.max(0, settings.radius))}px`,
  '--shadow': settings.card === 'flat' || settings.shadow === 0 ? 'none' : `0 ${settings.shadow}px ${settings.shadow * 3}px rgba(2, 8, 20, .15)`,
  ...(settings.background ? { '--bg': settings.background } : {})
}))
export function useTheme() { return { settings, style, persist } }
