/**
 * Thin typed wrapper over the API. Everything that talks to the backend goes through here, so the
 * components never touch fetch, and error handling is in one place instead of nowhere.
 */

export type Direction = 'above' | 'below'

export interface Rate {
  pair: string
  rate: number
  asOf: string
}

export interface Alert {
  id: string
  pair: string
  threshold: number
  direction: Direction
  triggered: boolean
  currentRate: number | null
  rateAsOf: string | null
  createdAt: string
  triggeredAt: string | null
  triggeredRate: number | null
}

export interface NewAlert {
  pair: string
  threshold: number
  direction: Direction
}

/** A failed request, carrying a message that is safe to put in front of the user. */
export class ApiError extends Error {}

export const api = {
  rates: () => request<Rate[]>('/api/rates'),

  alerts: () => request<Alert[]>('/api/alerts'),

  createAlert: (alert: NewAlert) =>
    request<Alert>('/api/alerts', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(alert),
    }),

  deleteAlert: (id: string) => request<void>(`/api/alerts/${id}`, { method: 'DELETE' }),
}

async function request<T>(url: string, init?: RequestInit): Promise<T> {
  let response: Response

  try {
    response = await fetch(url, init)
  } catch {
    throw new ApiError('Could not reach the server. Is the backend running?')
  }

  if (!response.ok) {
    throw new ApiError(await describeFailure(response))
  }

  return response.status === 204 ? (undefined as T) : ((await response.json()) as T)
}

/**
 * Turns an ASP.NET ProblemDetails body into a sentence. Validation errors come back keyed by field;
 * the user cares about the messages, not the keys.
 */
async function describeFailure(response: Response): Promise<string> {
  try {
    const problem = await response.json()
    const validationMessages = Object.values(problem?.errors ?? {}).flat() as string[]

    if (validationMessages.length > 0) {
      return validationMessages.join(' ')
    }

    if (problem?.detail || problem?.title) {
      return problem.detail ?? problem.title
    }
  } catch {
    // Not a JSON body - fall through to the generic message.
  }

  return `The server returned ${response.status}.`
}
