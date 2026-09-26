import { apiRequest } from '../http'
export type City = { id: string; code: string; name: string; enabled: boolean; provinceId?: string | null }
export type Region = { id: string; code: string; name: string; enabled: boolean }
export type BusinessSystem = { id: string; cityId: string; code: string; name: string; enabled: boolean }
export type BusinessFunction = { id: string; systemId: string; code: string; name: string }
export const resourceRequest = <T>(token: string, path: string, body?: object) => apiRequest<T>(`business-resources/${path}`, token, body ?
  { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})
