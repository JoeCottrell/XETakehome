import { reactive } from 'vue'
import { ApiError, api, type Alert, type NewAlert, type Rate } from './api'

/**
 * Shared app state. Still a plain reactive object rather than a store library: there is one screen
 * and a handful of fields, and every component reads the same thing. The moment this needs slices,
 * devtools or server-side state it should become a proper store.
 */
export const state = reactive({
  rates: [] as Rate[],
  alerts: [] as Alert[],
  lastUpdated: '',
  /** Message shown to the user when a request fails; empty when all is well. */
  error: '',
  busy: false,
})

export async function refresh(): Promise<void> {
  await Promise.all([loadRates(), loadAlerts()])
  state.lastUpdated = new Date().toLocaleTimeString()
}

export async function loadRates(): Promise<void> {
  await run(async () => {
    state.rates = await api.rates()
  })
}

export async function loadAlerts(): Promise<void> {
  await run(async () => {
    state.alerts = await api.alerts()
  })
}

export async function createAlert(alert: NewAlert): Promise<boolean> {
  return run(async () => {
    const created = await api.createAlert(alert)
    state.alerts = [...state.alerts, created]
  })
}

export async function deleteAlert(id: string): Promise<boolean> {
  return run(async () => {
    await api.deleteAlert(id)
    state.alerts = state.alerts.filter((alert) => alert.id !== id)
  })
}

/**
 * Runs an action, surfacing a failure as a message instead of an unhandled rejection.
 * Returns whether it succeeded, which is all the caller ever needs to know.
 */
async function run(action: () => Promise<void>): Promise<boolean> {
  state.busy = true

  try {
    await action()
    state.error = ''
    return true
  } catch (error) {
    state.error = error instanceof ApiError ? error.message : 'Something went wrong.'
    return false
  } finally {
    state.busy = false
  }
}
