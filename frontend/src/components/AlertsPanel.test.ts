import { fireEvent, render, within } from '@testing-library/vue'
import { expect, test } from 'vitest'
import AlertsPanel from './AlertsPanel.vue'
import type { Alert } from '../api'

function alert(overrides: Partial<Alert> = {}): Alert {
  return {
    id: 'a1',
    pair: 'USD/CAD',
    threshold: 1.3,
    direction: 'above',
    triggered: false,
    currentRate: 1.2899,
    rateAsOf: '2026-01-15T09:30:00Z',
    createdAt: '2026-01-15T09:00:00Z',
    triggeredAt: null,
    triggeredRate: null,
    ...overrides,
  }
}

function renderPanel(alerts: Alert[]) {
  return render(AlertsPanel, { props: { alerts, pairs: ['USD/CAD'], busy: false } })
}

test('an empty list says so', () => {
  const { getByText } = renderPanel([])
  expect(getByText(/no alerts yet/i)).toBeTruthy()
})

test('a waiting alert shows its rule and the current rate', () => {
  const { getByText } = renderPanel([alert()])

  expect(getByText('USD/CAD above 1.3')).toBeTruthy()
  expect(getByText('Waiting')).toBeTruthy()
  expect(getByText('Now 1.2899')).toBeTruthy()
})

test('a triggered alert is marked and says what fired it', () => {
  const { getByText, container } = renderPanel([
    alert({ triggered: true, currentRate: 1.37, triggeredRate: 1.3712, triggeredAt: '2026-01-15T09:31:00Z' }),
  ])

  expect(getByText('Triggered')).toBeTruthy()
  expect(getByText(/fired at 1\.3712/)).toBeTruthy()
  expect(container.querySelector('.alert.triggered')).not.toBeNull()
})

test('triggered alerts are listed first', () => {
  const { container } = renderPanel([
    alert({ id: 'waiting', pair: 'EUR/USD' }),
    alert({ id: 'fired', pair: 'GBP/USD', triggered: true, triggeredRate: 1.4, triggeredAt: '2026-01-15T09:31:00Z' }),
  ])

  const rules = [...container.querySelectorAll('.alert-rule')].map((node) => node.textContent)
  expect(rules[0]).toContain('GBP/USD')
})

test('a rate the API could not provide is called out rather than shown as zero', () => {
  const { getByText } = renderPanel([alert({ currentRate: null })])
  expect(getByText('Now unavailable')).toBeTruthy()
})

test('submitting the form emits a create with a numeric threshold', async () => {
  const { getByLabelText, getByRole, emitted } = renderPanel([])

  await fireEvent.update(getByLabelText('Pair'), 'gbp/usd')
  await fireEvent.update(getByLabelText('Direction'), 'below')
  await fireEvent.update(getByLabelText('Threshold'), '1.2345')
  await fireEvent.click(getByRole('button', { name: /add alert/i }))

  expect(emitted().create).toEqual([[{ pair: 'gbp/usd', threshold: 1.2345, direction: 'below' }]])
})

test('the form will not submit without a pair and a threshold', async () => {
  const { getByRole, emitted } = renderPanel([])

  await fireEvent.click(getByRole('button', { name: /add alert/i }))

  expect(emitted().create).toBeUndefined()
})

test('delete emits the id of the alert it sits next to', async () => {
  const { container, emitted } = renderPanel([alert({ id: 'to-remove' })])
  const row = container.querySelector('.alert') as HTMLElement

  await fireEvent.click(within(row).getByRole('button', { name: /delete/i }))

  expect(emitted().remove).toEqual([['to-remove']])
})
