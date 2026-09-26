import { apiRequest } from '../http'

export type ManagedNode = { id: string; name: string; nodeKind: string; osPlatform: string; status: string; networkZone: string | null;
  lastHeartbeatAt: string | null; cpuUsage: number | null; memoryUsage: number | null; reportedAvailableSlots: number | null; capabilities: Array<{ code: string }> }
export type WorkerSlot = { id: string; nodeId: string; slotName: string; enabled: boolean; executionId: string | null }
export function nodeRequest<T>(token: string, path: string, method = 'GET', body?: object): Promise<T> {
  return apiRequest<T>(path, token, { method,
    ...(body ? { headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body) } : {}) })
}
