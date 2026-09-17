# Notes

Backend track. Roughly three hours.

The alert feature is server-side: a data model, create/list/read/delete endpoints, and evaluation
against fresh rates from the Xe API. The stub controller is gone. The UI was extended far enough to
create, list and delete alerts and to show which ones have triggered.

## The data model

```
Alert
  Id            Guid
  Pair          CurrencyPair    (two ISO codes, BASE/QUOTE, normalised and validated on the way in)
  Threshold     decimal
  Direction     above | below
  CreatedAt     DateTimeOffset
  TriggeredAt   DateTimeOffset?   null until it fires
  TriggeredRate decimal?          the rate that fired it
```

`Triggered` is not a stored flag, it is `TriggeredAt is not null`, so there is one source of truth
and no way to end up with a triggered alert that cannot say when or why it fired.

Three choices are worth defending:

**Triggering latches.** The stub recomputed `triggered` from the current rate on every read, so an
alert would quietly un-trigger as soon as the rate moved back. That loses exactly the event the user
asked to be told about. Here, crossing the threshold is a thing that happened: it is stamped with a
time and a rate and it stays. The cost is that an alert is one-shot - there is no re-arm - which is
the right default for "tell me when", and a `POST /api/alerts/{id}/reset` is a small addition if
repeat firing is ever wanted.

**Comparisons are strict.** A rate exactly on the threshold has not gone *above* it, so `1.3000` does
not fire an `above 1.3000` alert. Either rule is defensible; the point is that it is decided,
documented and tested rather than accidental.

**A pair is a value, not a string.** `CurrencyPair` parses once at the edge, upper-cases, and rejects
anything that is not two different three-letter codes. Everything below validation works with a value
that cannot be malformed, and `"gbp/usd"` and `"GBP/USD"` are the same key in a dictionary. This is
also why alerts are not restricted to the three pairs on the board: the provider prices whatever pair
it is handed, so the restriction would have been extra code rather than less.

## Storage: in memory, behind an interface

`InMemoryAlertStore` is a `ConcurrentDictionary` and everything is lost on restart. Nothing in this
app needs otherwise: there is no user identity to scope alerts to, no notification to deliver, and no
requirement that an alert outlive the process. A database would have added migrations, a connection
string, a container to run it in and a slower test suite for no behaviour the reviewer can see.

What it costs: alerts do not survive a restart or a deploy, and a second instance would not see the
first one's alerts, so this design is single-node only. The seam that makes it swappable is
`IAlertStore`, which is deliberately async even though the dictionary is not - a real store would be,
and I would rather have the ceremony now than a breaking change later. Replacing it means one class
plus a DI line; nothing above it knows where alerts live.

Replacement writes are a compare-and-swap (`TryReplaceAsync(previous, updated)`), so two concurrent
evaluations cannot both stamp an alert and a deleted alert cannot be resurrected by an evaluation
that was already in flight.

## Evaluation on read, not on a timer

Alerts are evaluated when they are listed or fetched, and on creation, so an alert created on the
wrong side of the current rate comes back already triggered instead of looking inert.

The alternative is a `BackgroundService` polling every N seconds. That is what a real product needs,
and it is the first thing I would add - but only alongside somewhere for a trigger to *go* (email,
push, a webhook). Without that, a poller would burn a metered API quota all day so that a screen
nobody is looking at can be right. The honest cost of the current design: a crossing that happens and
reverses between two reads is missed. Storing the triggered state rather than computing it is what
makes the switch to scheduled evaluation a small change.

## The rates code

The original controller created three `HttpClient`s per request, blocked on `.Result` three times in
sequence and repeated the same twelve lines for each pair. That is socket exhaustion and thread-pool
starvation waiting for load, so it was worth fixing before building on it:

- `IRateProvider` as the seam, so alert evaluation is testable without the network.
- `XeRateProvider` is a typed `HttpClient` with credentials applied once, grouping pairs by base
  currency into a single `convert_from` call and running the groups concurrently.
- `CachingRateProvider` reuses a rate for a configurable few seconds (default 10). The board polls on
  every page load and every alert list needs rates; Xe bills per request. Concurrent misses for the
  same pair can both reach the API - collapsing them behind a lock trades a rare duplicate call for a
  shared failure path, which is not a good trade at this scale.
- A pair that cannot be priced is *absent* from the result rather than an exception, so one bad pair
  cannot take the board or the alert list down. If nothing resolves at all, `/api/rates` answers 503
  rather than serving an empty board that looks like "no pairs configured", and an alert with no rate
  is shown as-is with "unavailable" instead of a misleading zero.
- The board's pairs and the credentials moved into bound, validated options, so a missing API key
  fails at start-up instead of on the first request.

## The frontend

The brief asks for the simplest wiring that works on this track, so the UI is deliberately modest,
but two of the rough edges were in the way: `fetch` inside the component with no error path, and
three near-identical getters doing a linear search per card.

`api.ts` is now the only thing that talks to HTTP; it types the responses and turns a ProblemDetails
body into a sentence, so the validation messages the backend already writes reach the user instead of
the console. `state.ts` holds the shared state and the actions, with failures landing in `state.error`
rather than in an unhandled rejection. The board renders whatever pairs the API returns. `AlertsPanel`
creates, lists and deletes alerts, sorts triggered ones to the top and shows the rate and time that
fired them. Pinia was installed and never used, so it is gone; one screen with a handful of fields
does not need a store library, and it can be added back deliberately if that changes.

## Tests

`dotnet test RateAlerts.sln` (65 tests) and `npm test` in `frontend/` (16). Both also run in CI on
every push, which is the "going further" option I picked - tests nobody runs stop being true.

They go where a bug would be expensive or silent: the threshold rule including the boundary and the
latching behaviour, the service's evaluate-and-store loop (including a rate that is unavailable, and
that rates are requested once per distinct pair), the store's compare-and-swap, the cache TTL, and
the parsing of a `convert_from` payload with entries that are malformed. On the frontend, the state
module against a mocked fetch including the failure paths, and the alerts panel as a component.

Not tested: the controllers, beyond being thin enough to read in one go. HTTP-level tests with
`WebApplicationFactory` are the gap I would close first.

## Deliberately left undone

- **No persistence, no users, no auth.** Alerts are global to the process. Any real version needs an
  owner on the alert before it needs a database.
- **No notifications.** "Triggered" is only visible if you look. That and scheduled evaluation are one
  piece of work, not two.
- **No re-arm or edit.** An alert fires once and can be deleted. Adding a reset is small; deciding
  whether repeat firing should be debounced is not, so I left it.
- **Currency codes are only checked for shape.** `ABC/XYZ` is accepted and then never prices, because
  I did not want a momentary API outage to look like a validation error at creation time. Validating
  against Xe's currency list (cached at start-up) is the fix.
- **The API key is still in `appsettings.json`.** It was given that way and the options are bound so
  `Xecd__ApiKey` overrides it from the environment, but a real deployment gets it from a secret store
  and the file gets a placeholder.
- **The CSS is the original CSS** plus the few classes the alert list needed.
- I rewrote `README.md` to describe what the app is now; the stub-API section went with the stub.

## What I would do next, in order

1. HTTP-level tests for the three endpoints, so the contract is pinned rather than implied.
2. A `BackgroundService` that evaluates alerts on a schedule, plus a delivery channel for a trigger -
   at which point "triggered" means something when nobody is watching.
3. An owner on the alert, and persistence behind the existing `IAlertStore` seam.
4. Validate currency codes against Xe's supported list at creation time.
5. Let the UI refresh itself (polling or SSE) instead of relying on the Refresh button.

## AI tools

I used Claude Code (Claude Opus 5) throughout, as I normally do, and reviewed everything it produced.

What I directed rather than accepted: the latching trigger model (its first instinct was to keep the
stub's recompute-on-read behaviour, which is wrong for this feature), evaluating on read instead of
adding a background poller that would have been scope the brief did not ask for, keeping the
in-memory store rather than reaching for a database, and the strict boundary comparison. It is good
at the mechanical refactors - the typed `HttpClient`, the caching decorator, the test scaffolding -
and those are most of the volume here. It is not good at knowing when to stop, which is why the list
above of things deliberately left undone is as long as it is.
