import { apiRequest } from '../http'

export type AuthSession = { accessToken: string; userId: string; userName: string; displayName: string; roles: string[] }
export type AuthUser = { userId: string; userName: string; roles: string[] }

export const login = (userName: string, password: string) => apiRequest<AuthSession>('auth/login', null, {
  method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ userName, password })
})
export const currentUser = (token: string) => apiRequest<AuthUser>('auth/me', token)
