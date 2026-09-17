import { beforeEach, describe, expect, test, vi } from 'vitest'
import { createAlert, deleteAlert, loadRates, refresh, state } from './state'

function jsonResponse(body: unknown, status = 200) {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: () => Promise.resolve(body),
  } as Response
}

beforeEach(() => {
  state.rates = []
  state.alerts = []
  state.error = ''
  state.lastUpdated = ''
  state.busy = false
})

describe('loading', () => {
  test('refresh fills rates and alerts and stamps the time', async () => {
    const fetchMock = vi.fn((url: string) =>
      Promise.resolve(
        url === '/api/rates'
          ? jsonResponse([{ pair: 'USD/CAD', rate: 1.365, asOf: '2026-01-15T09:30:00Z' }])
          : jsonResponse([{ id: 'a1', pair: 'USD/CAD', threshold: 1.3, direction: 'above', triggered: true }]),
      ),
    )
    vi.stubGlobal('fetch', fetchMock)

    await refresh()

    expect(state.rates).toHaveLength(1)
    expect(state.alerts[0].triggered).toBe(true)
    expect(state.lastUpdated).not.toBe('')
    expect(state.error).toBe('')
  })

  test('a failed request leaves a message instead of throwing', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(jsonResponse({ title: 'Rates are unavailable', detail: 'Try again shortly.' }, 503)),
      ),
    )

    await loadRates()

    expect(state.error).toBe('Try again shortly.')
    expect(state.rates).toEqual([])
  })

  test('an unreachable server is reported in plain language', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))))

    await loadRates()

    expect(state.error).toMatch(/backend/i)
  })
})

describe('alerts', () => {
  test('a created alert is added without a reload', async () => {
    const created = { id: 'a2', pair: 'GBP/USD', threshold: 1.3, direction: 'below', triggered: false }
    const fetchMock = vi.fn(() => Promise.resolve(jsonResponse(created, 201)))
    vi.stubGlobal('fetch', fetchMock)

    const ok = await createAlert({ pair: 'GBP/USD', threshold: 1.3, direction: 'below' })

    expect(ok).toBe(true)
    expect(state.alerts).toEqual([created])
    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit]
    expect(url).toBe('/api/alerts')
    expect(init.method).toBe('POST')
    expect(JSON.parse(init.body as string)).toEqual({ pair: 'GBP/USD', threshold: 1.3, direction: 'below' })
  })

  test('validation errors from the API are surfaced verbatim', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(() =>
        Promise.resolve(
          jsonResponse(
            {
              title: 'One or more validation errors occurred.',
              errors: { Pair: ['Pair must be two different ISO currency codes in BASE/QUOTE form, for example GBP/USD.'] },
            },
            400,
          ),
        ),
      ),
    )

    const ok = await createAlert({ pair: 'nonsense', threshold: 1, direction: 'above' })

    expect(ok).toBe(false)
    expect(state.error).toContain('BASE/QUOTE')
    expect(state.alerts).toEqual([])
  })

  test('a deleted alert is dropped from the list', async () => {
    state.alerts = [
      { id: 'a1', pair: 'USD/CAD' } as never,
      { id: 'a2', pair: 'GBP/USD' } as never,
    ]
    const fetchMock = vi.fn(() => Promise.resolve({ ok: true, status: 204 } as Response))
    vi.stubGlobal('fetch', fetchMock)

    await deleteAlert('a1')

    expect(state.alerts.map((alert) => alert.id)).toEqual(['a2'])
    expect(fetchMock).toHaveBeenCalledWith('/api/alerts/a1', { method: 'DELETE' })
  })
})
