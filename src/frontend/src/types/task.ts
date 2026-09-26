export type Execution = { id: string; taskItemId: string; status: string; error?: string | null }
export type TaskItem = { id: string; sequence: number; status: string; retryCount: number; resultJson?: string | null; executions: Execution[] }
export type Timeline = { status: string; nodeId?: string | null; workerSlotId?: string | null; lease?: { released: boolean; expiresAt: string; lastHeartbeatAt: string } | null; events: Array<{ sequence: number; eventType: string; stepId?: string | null; message: string; createdAt: string }> }
export type ExecutionEntry = { id: string; sequence: number; level: string; eventType: string; stepId?: string; message: string; sensitive: boolean }
