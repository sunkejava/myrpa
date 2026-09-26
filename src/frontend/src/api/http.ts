export class ApiError extends Error {
  constructor(public readonly status: number, message: string) { super(message) }
}

export async function apiRequest<T>(path: string, token: string | null, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers)
  if (token) headers.set('Authorization', `Bearer ${token}`)
  const response = await fetch(`/api/${path.replace(/^\//, '')}`, { ...init, headers })
  if (!response.ok) {
    const body: unknown = await response.json().catch(() => null)
    const message = body && typeof body === 'object' && 'message' in body && typeof body.message === 'string'
      ? body.message : `API 请求失败 (${response.status})`
    const details = body && typeof body === 'object' && 'errors' in body && Array.isArray(body.errors)
      ? body.errors.filter((item): item is string => typeof item === 'string').join('；') : ''
    throw new ApiError(response.status, details ? `${message}：${details}` : message)
  }
  if (response.status === 204) return undefined as T
  const content = await response.text()
  return (content ? JSON.parse(content) : undefined) as T
}
