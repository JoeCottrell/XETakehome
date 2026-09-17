<script setup lang="ts">
import type { Rate } from '../api'

defineProps<{ rates: Rate[] }>()

/** "USD/CAD" reads as "1 USD in CAD" - works for any pair, unlike the hard-coded captions. */
function caption(pair: string): string {
  const [base, quote] = pair.split('/')
  return `1 ${base} in ${quote}`
}
</script>

<template>
  <section class="cards">
    <div v-for="rate in rates" :key="rate.pair" class="card">
      <div class="pair">{{ rate.pair.replace('/', ' / ') }}</div>
      <div class="rate">{{ rate.rate.toFixed(4) }}</div>
      <div class="caption">{{ caption(rate.pair) }}</div>
    </div>

    <p v-if="rates.length === 0" class="empty">Waiting for rates...</p>
  </section>
</template>
