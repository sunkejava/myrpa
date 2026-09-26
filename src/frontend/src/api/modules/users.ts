import { apiRequest } from '../http'

export type UserAccount = { id: string; userName: string; displayName: string; enabled: boolean }
export type UserRole = { id: string; name: string; displayName: string; enabled: boolean }
export function userRequest<T>(token: string, path: string, method = 'GET', body?: object): Promise<T> {
  return apiRequest<T>(`user-management/${path}`, token, {
    method,
    ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})
  })
}
