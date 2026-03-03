# Architecture Decisions — Athena Finance

> This file is append-only. New decisions are added at the bottom with a version and date.
> Never edit or remove existing entries.

---

## v0.1 — 2026-03-03

| # | Decision | Rationale |
|---|----------|-----------|
| 1 | **Two separate repos**: `athena-finance-api` (backend) and `athena-finance-web` (frontend) | Different release cycles, different stacks, easier CI/CD per component. A third repo `athena-finance-ios` will be added later for the iOS app. |
| 2 | **Backend stack: .NET 10 Web API (C#)** | Bertrand's choice. Strong typing, mature ecosystem, excellent for financial data APIs. |
| 3 | **Frontend stack: React + Vite + TypeScript + Tailwind CSS** | Fast dev experience, type-safe, component-friendly. Vite chosen over CRA for build speed. |
| 4 | **Primary data source: Finnhub** | Free tier covers equities + forex + real-time quotes. WebSocket support is useful for future iOS widget. Multiple providers may be added later as asset universe expands. |
| 5 | **Database: PostgreSQL with EF Core (Npgsql)** | Reliable, excellent time-series query support. TimescaleDB extension considered for future if performance requires it. SQLite ruled out for production suitability. |
| 6 | **EOD price storage** | End-of-day prices are stored locally in the database. Live quotes are fetched on-demand from Finnhub (not persisted). |
| 7 | **Initial asset classes: Equity, Bond, Forex** | ETFs deferred to a later version. Asset type stored as a string enum in the DB for readability. |
| 8 | **Versioning strategy: semantic versioning (semver)** | `main` = stable tagged releases. `develop` = integration. `feature/*` = individual features. Merge to `develop`, test, then release to `main` with a tag. |
| 9 | **No authentication in v0.1** | Single-user, private/local deployment. Auth will be revisited when the iOS app or multi-user scenarios are introduced. |
| 10 | **CORS policy: localhost:5173 in dev** | Frontend runs on Vite's default port. CORS locked to local dev only; production policy TBD. |
| 11 | **Auto-migration on startup (dev only)** | EF Core migrations run automatically in `Development` environment for convenience. Production will require explicit migration steps. |
| 12 | **Frontend auto-refresh interval: 30 seconds** | Balance between live feel and Finnhub free-tier rate limits. Configurable in a future version. |
