# Rate Alerts

A small app that displays live exchange rates, backed by the Xe Currency Data API, with rate alerts
on top: set a threshold for a currency pair and the app tells you when the rate has passed it.

`NOTES.md` covers the design decisions, the trade-offs behind them, and what I would do next.

## Prerequisites

- .NET 10 SDK
- Node.js 20 or newer

## Run it

Backend (from the repo root):

```
cd backend
dotnet run
```

The API starts on `http://localhost:5180`. API credentials are already configured in `backend/appsettings.json`; you do not need to sign up for anything.

Frontend (in a second terminal):

```
cd frontend
npm install
npm run dev
```

The app runs on `http://localhost:5173` and proxies `/api` calls to the backend, so start the backend first.

## Tests

```
dotnet test RateAlerts.sln     # backend, from the repo root
cd frontend && npm test        # frontend
```

Both suites also run in GitHub Actions on every push (`.github/workflows/ci.yml`). Nothing in either
suite touches the network.

## What's here

- `backend/` - a .NET Web API.
  - `Rates/` fetches live rates from the Xe Currency Data API behind `IRateProvider`, with a short-lived cache.
  - `Alerts/` is the alert feature: the model, the evaluation rule, the store and the service.
  - `Controllers/` are thin: validate, delegate, map to the wire shape.
- `tests/RateAlerts.Api.Tests/` - unit tests for the above.
- `frontend/` - a Vue 3 + TypeScript app (Vite). `api.ts` talks to the backend, `state.ts` holds the
  shared state, and the components render the board and the alerts.

Xe Currency Data API documentation: https://xecdapi.xe.com/docs/v1/

## API

| Method | Path | Body | Returns |
| --- | --- | --- | --- |
| GET | `/api/rates` | - | `[{ pair, rate, asOf }]` for the pairs in `RateBoard:Pairs`, or `503` if none can be fetched |
| GET | `/api/alerts` | - | every alert, evaluated against current rates |
| GET | `/api/alerts/{id}` | - | one alert, or `404` |
| POST | `/api/alerts` | `{ "pair": "USD/CAD", "threshold": 1.30, "direction": "above" }` | the created alert, `201` |
| DELETE | `/api/alerts/{id}` | - | `204`, or `404` if unknown |

An alert looks like this:

```json
{
  "id": "0b3f...",
  "pair": "USD/CAD",
  "threshold": 1.30,
  "direction": "above",
  "triggered": true,
  "currentRate": 1.3650,
  "rateAsOf": "2026-01-15T09:30:00+00:00",
  "createdAt": "2026-01-15T09:00:00+00:00",
  "triggeredAt": "2026-01-15T09:05:00+00:00",
  "triggeredRate": 1.3612
}
```

`direction` is `above` or `below`, and both are strict: a rate exactly on the threshold does not
trigger. `pair` is any two different ISO currency codes in `BASE/QUOTE` form - it is not limited to
the three pairs on the board, as long as the API can price it. Triggering latches: once an alert has
fired, `triggered` stays true and `triggeredAt` / `triggeredRate` record what fired it, even if the
rate moves back. Invalid input comes back as a `400` ProblemDetails listing every problem at once.

## Configuration

`backend/appsettings.json`, overridable by environment variables in the usual ASP.NET way
(`Xecd__CacheSeconds=0`, for example):

| Setting | Meaning |
| --- | --- |
| `Xecd:AccountId`, `Xecd:ApiKey` | Xe Currency Data API credentials |
| `Xecd:BaseUrl` | API base address |
| `Xecd:CacheSeconds` | How long a fetched rate is reused; `0` disables caching |
| `Xecd:TimeoutSeconds` | Per-request timeout to the API |
| `RateBoard:Pairs` | The pairs shown on the board |

## Notes

- Alerts are held in memory and do not survive a restart. `NOTES.md` explains why, and what changing
  it would involve.
- If a rate never resolves, check the backend terminal for errors and that it is running on port 5180.
