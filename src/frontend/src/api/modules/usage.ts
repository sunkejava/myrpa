import { apiRequest } from '../http'
export type UsageSummary = { calls: number; inputTokens: number; outputTokens: number; totalTokens: number }
export type UsageEntry = { id: string; taskId: string | null; model: string; providerId: string; inputTokens: number; outputTokens: number; totalTokens: number; occurredAt: string }
export function listUsage(token: string, taskId: string, afterId?: string) {
  const params = new URLSearchParams({ take: '50' })
  if (taskId.trim()) params.set('taskId', taskId.trim())
  if (afterId) params.set('afterId', afterId)
  return apiRequest<{ items: UsageEntry[]; nextAfterId: string | null }>(`llm-usage?${params}`, token)
}
export function usageSummary(token: string, taskId: string) {
  const params = new URLSearchParams()
  if (taskId.trim()) params.set('taskId', taskId.trim())
  return apiRequest<UsageSummary>(`llm-usage/summary${params.size ? `?${params}` : ''}`, token)
}
