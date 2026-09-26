import { apiRequest } from '../http'

export type WorkflowResource = { id: string; code: string; name: string; enabled?: boolean }
export type WorkflowItem = { id: string; name: string; businessFunctionId: string; status: string }
export type WorkflowVersion = { id: string; version: number; published: boolean }

export function workflowRequest<T>(token: string, path: string, method = 'GET', body?: object): Promise<T> {
  return apiRequest<T>(path, token, {
    method,
    ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {})
  })
}
