import { apiRequest } from '../http'
export type AuditEntry = { id: string; createdAt: string; actor: string; action: string; resource: string; resourceId: string; result: string; summary: string }
export function listAudit(token: string, filters: Record<string, string>, limit = 100) {
  const params = new URLSearchParams({ limit: String(limit) })
  for (const key of ['actor', 'resource']) if (filters[key]?.trim()) params.set(key, filters[key].trim())
  return apiRequest<AuditEntry[]>(`audit?${params}`, token)
}
