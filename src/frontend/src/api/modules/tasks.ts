import { apiRequest } from '../http'
export type TaskListItem = { id: string; name: string; status: string; approvalStatus?: string | null; total: number; succeeded: number; failed: number }
export const listTasks = (token: string) => apiRequest<TaskListItem[]>('tasks', token)
export const taskAction = (token: string, id: string, action: string) => apiRequest<unknown>(`tasks/${encodeURIComponent(id)}/${action}`, token,
  { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' })
