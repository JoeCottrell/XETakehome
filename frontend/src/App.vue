<script setup lang="ts">
import { computed, onMounted } from 'vue'
import AlertsPanel from './components/AlertsPanel.vue'
import RateBoard from './components/RateBoard.vue'
import { createAlert, deleteAlert, loadAlerts, refresh, state } from './state'
import type { NewAlert } from './api'

/** The board's pairs are whatever the API returns; the alert form offers them as suggestions. */
const boardPairs = computed(() => state.rates.map((rate) => rate.pair))

async function add(alert: NewAlert) {
  await createAlert(alert)
}

async function remove(id: string) {
  await deleteAlert(id)
}

onMounted(() => {
  refresh()
})

defineExpose({ refresh, loadAlerts })
</script>

<template>
  <main class="page">
    <header class="header">
      <h1>Xe Rate Board</h1>
      <span class="updated" v-if="state.lastUpdated">Last updated {{ state.lastUpdated }}</span>
    </header>

    <p v-if="state.error" class="error" role="alert">{{ state.error }}</p>

    <RateBoard :rates="state.rates" />

    <button class="refresh" :disabled="state.busy" @click="refresh()">Refresh</button>

    <AlertsPanel
      :alerts="state.alerts"
      :pairs="boardPairs"
      :busy="state.busy"
      @create="add"
      @remove="remove"
    />
  </main>
</template>

<style>
* {
  box-sizing: border-box;
}

body {
  margin: 0;
  font-family: 'Segoe UI', system-ui, sans-serif;
  background: #f4f6f8;
  color: #1a2233;
}

.page {
  max-width: 860px;
  margin: 0 auto;
  padding: 32px 20px;
}

.header {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-bottom: 24px;
}

h1 {
  font-size: 1.6rem;
  margin: 0;
}

h2 {
  font-size: 1.1rem;
  margin: 0 0 12px;
}

.updated {
  font-size: 0.85rem;
  color: #66718a;
}

.error {
  margin: 0 0 16px;
  padding: 10px 14px;
  border: 1px solid #f0c2c2;
  border-radius: 8px;
  background: #fdf2f2;
  color: #8a2222;
  font-size: 0.9rem;
}

.cards {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
}

.card {
  background: #ffffff;
  border: 1px solid #e1e6ee;
  border-radius: 10px;
  padding: 20px;
}

.pair {
  font-size: 0.9rem;
  font-weight: 600;
  color: #66718a;
  letter-spacing: 0.04em;
}

.rate {
  font-size: 2rem;
  font-weight: 700;
  margin: 8px 0 4px;
  font-variant-numeric: tabular-nums;
}

.caption,
.empty {
  font-size: 0.8rem;
  color: #8a93a8;
}

.refresh {
  margin-top: 24px;
  padding: 10px 18px;
  border: none;
  border-radius: 8px;
  background: #16345c;
  color: #ffffff;
  font-size: 0.9rem;
  cursor: pointer;
}

.refresh:hover:not(:disabled) {
  background: #1d4377;
}

.refresh:disabled {
  opacity: 0.6;
  cursor: default;
}

.alerts {
  margin-top: 36px;
}

.alert-form {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  align-items: flex-end;
  margin-bottom: 16px;
}

.alert-form label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 0.8rem;
  color: #66718a;
}

.alert-form input,
.alert-form select {
  padding: 8px 10px;
  border: 1px solid #d5dce7;
  border-radius: 8px;
  font-size: 0.9rem;
  background: #ffffff;
}

.alert-form button {
  padding: 9px 16px;
  border: none;
  border-radius: 8px;
  background: #16345c;
  color: #ffffff;
  font-size: 0.9rem;
  cursor: pointer;
}

.alert-form button:disabled {
  opacity: 0.6;
  cursor: default;
}

.alert-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: 10px;
}

.alert {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 4px 16px;
  align-items: center;
  padding: 14px 16px;
  background: #ffffff;
  border: 1px solid #e1e6ee;
  border-radius: 10px;
}

.alert.triggered {
  border-color: #f0b429;
  background: #fffaf0;
}

.alert-main {
  display: flex;
  align-items: center;
  gap: 10px;
}

.alert-rule {
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}

.alert-detail {
  grid-column: 1;
  font-size: 0.8rem;
  color: #8a93a8;
}

.badge {
  font-size: 0.7rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  padding: 3px 8px;
  border-radius: 999px;
}

.badge-triggered {
  background: #f0b429;
  color: #3d2c00;
}

.badge-waiting {
  background: #e7ecf4;
  color: #66718a;
}

.link {
  grid-row: 1 / span 2;
  grid-column: 2;
  border: none;
  background: none;
  color: #8a2222;
  font-size: 0.85rem;
  cursor: pointer;
  padding: 0;
}

.link:disabled {
  opacity: 0.6;
  cursor: default;
}
</style>
