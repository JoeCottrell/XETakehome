<script setup lang="ts">
import { computed, ref } from 'vue'
import type { Alert, Direction } from '../api'

const props = defineProps<{ alerts: Alert[]; pairs: string[]; busy: boolean }>()
const emit = defineEmits<{
  create: [alert: { pair: string; threshold: number; direction: Direction }]
  remove: [id: string]
}>()

const pair = ref('')
const direction = ref<Direction>('above')
// Vue casts a number input to a number, so this is a number or the empty string, never a numeric string.
const threshold = ref<number | ''>('')

const canSubmit = computed(() => pair.value.trim() !== '' && threshold.value !== '')

// Triggered alerts first: they are the reason anyone opened this page.
const ordered = computed(() =>
  [...props.alerts].sort((a, b) => Number(b.triggered) - Number(a.triggered)),
)

function submit() {
  if (!canSubmit.value) {
    return
  }

  emit('create', {
    pair: pair.value.trim(),
    threshold: Number(threshold.value),
    direction: direction.value,
  })

  threshold.value = ''
}

function describe(alert: Alert): string {
  return `${alert.pair} ${alert.direction} ${alert.threshold}`
}

function format(rate: number | null): string {
  return rate === null ? 'unavailable' : rate.toFixed(4)
}

function when(timestamp: string | null): string {
  return timestamp === null ? '' : new Date(timestamp).toLocaleString()
}
</script>

<template>
  <section class="alerts">
    <h2>Alerts</h2>

    <form class="alert-form" @submit.prevent="submit">
      <label>
        Pair
        <input v-model="pair" list="known-pairs" placeholder="GBP/USD" aria-label="Pair" />
        <datalist id="known-pairs">
          <option v-for="known in pairs" :key="known" :value="known" />
        </datalist>
      </label>

      <label>
        Direction
        <select v-model="direction" aria-label="Direction">
          <option value="above">goes above</option>
          <option value="below">goes below</option>
        </select>
      </label>

      <label>
        Threshold
        <input v-model="threshold" type="number" step="0.0001" min="0" placeholder="1.3000" aria-label="Threshold" />
      </label>

      <button type="submit" :disabled="!canSubmit || busy">Add alert</button>
    </form>

    <p v-if="alerts.length === 0" class="empty">No alerts yet. Add one above.</p>

    <ul v-else class="alert-list">
      <li v-for="alert in ordered" :key="alert.id" :class="['alert', { triggered: alert.triggered }]">
        <div class="alert-main">
          <span class="alert-rule">{{ describe(alert) }}</span>
          <span :class="['badge', alert.triggered ? 'badge-triggered' : 'badge-waiting']">
            {{ alert.triggered ? 'Triggered' : 'Waiting' }}
          </span>
        </div>

        <div class="alert-detail">
          <span>Now {{ format(alert.currentRate) }}</span>
          <span v-if="alert.triggered">
            - fired at {{ format(alert.triggeredRate) }} on {{ when(alert.triggeredAt) }}
          </span>
        </div>

        <button class="link" type="button" :disabled="busy" @click="emit('remove', alert.id)">
          Delete
        </button>
      </li>
    </ul>
  </section>
</template>
