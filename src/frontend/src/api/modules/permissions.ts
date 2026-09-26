import { apiRequest } from '../http'

export type PermissionUser = { id: string; userName: string; displayName: string; enabled: boolean }
export type PermissionRole = { id: string; name: string; displayName: string; enabled: boolean }
export type PermissionResource = { id: string; code: string; name: string; enabled?: boolean }
export type UserPolicy = { id: string; subjectId: string; cityId: string; systemId: string; functionId: string; action: string; enabled: boolean; denied: boolean }
export type RolePolicy = Omit<UserPolicy, 'subjectId'> & { roleId: string }

export function permissionRequest<T>(token: string, path: string, method = 'GET', body?: object): Promise<T> {
  return apiRequest<T>(path, token, {
    method,
    ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})
  })
}
