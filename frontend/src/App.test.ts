import { render, waitFor } from '@testing-library/vue'
import { expect, test, vi } from 'vitest'
import App from './App.vue'

test('loads rates and alerts on mount and shows them', async () => {
  const fetchMock = vi.fn((url: string) =>
    Promise.resolve({
      ok: true,
      status: 200,
      json: () =>
        Promise.resolve(
          url === '/api/rates'
            ? [
                { pair: 'USD/CAD', rate: 1.365, asOf: '2026-01-15T09:30:00Z' },
                { pair: 'GBP/USD', rate: 1.271, asOf: '2026-01-15T09:30:00Z' },
              ]
            : [
                {
                  id: 'a1',
                  pair: 'USD/CAD',
                  threshold: 1.3,
                  direction: 'above',
                  triggered: true,
                  currentRate: 1.365,
                  triggeredRate: 1.365,
                  triggeredAt: '2026-01-15T09:30:00Z',
                },
              ],
        ),
    } as Response),
  )
  vi.stubGlobal('fetch', fetchMock)

  const { getByText, container } = render(App)

  await waitFor(() => expect(container.querySelectorAll('.card')).toHaveLength(2))
  expect(getByText('1.3650')).toBeTruthy()
  expect(getByText('USD / CAD')).toBeTruthy()
  expect(getByText('Triggered')).toBeTruthy()
  const requested = fetchMock.mock.calls.map(([url]) => url)
  expect(requested).toContain('/api/rates')
  expect(requested).toContain('/api/alerts')
})

test('a backend that is down is reported, not swallowed', async () => {
  vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new TypeError('Failed to fetch'))))

  const { findByRole } = render(App)

  expect(await findByRole('alert')).toBeTruthy()
})
