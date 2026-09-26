import { apiRequest } from '../http'
export type Plan = { cityId: string; systemId: string; functionId: string; action: string; riskLevel: string; workflowId: string; workflowVersion: number; requiresConfirmation: boolean }
export type PlanResponse = { success: boolean; summary: string; ambiguities: string[]; plan: Plan | null }
const post = <T>(token: string, path: string, instruction: string, confirmed?: boolean) => apiRequest<T>(path, token,
  { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ instruction, ...(confirmed === undefined ? {} : { confirmed }) }) })
export const makePlan = (token: string, instruction: string) => post<PlanResponse>(token, 'agent/plan', instruction)
export const submitPlan = (token: string, instruction: string) => post<unknown>(token, 'agent/execute', instruction, true)
