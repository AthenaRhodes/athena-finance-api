# Changelog — athena-finance-api

> Append-only. Each beta version is documented below in reverse chronological order.

---

## v0.1.0-beta.5 — 2026-03-03

### Added
- `GetMetricsAsync` in `FinnhubService` — fetches performance metrics from Finnhub `/stock/metric`
- Watchlist response now includes `metrics`: YTD return, 5D/13W/26W/52W returns, 52W high/low with dates (equities only)

### Notes
- Historical candle data (`/stock/candle`) is not available on Finnhub free tier — metrics endpoint used instead

---

## v0.1.0-beta.4 — 2026-03-03

### Added
- `GET /api/search?q=` — symbol/company search via Finnhub, returns top 8 results
- `SecuritiesController` now auto-resolves name, exchange and currency from Finnhub profile when not provided (equities)
- Returns `400` if symbol not found on Finnhub instead of creating a ghost record

### Changed
- `CreateSecurityRequest`: `Name` is now optional (auto-resolved for equities)

---

## v0.1.0-beta.3 — 2026-03-03

### Added
- `GetProfileAsync` in `FinnhubService` — fetches company profile from Finnhub `/stock/profile2`
- Watchlist response now includes `marketCapMillions`, `industry` and `logo` (Equity only)

---

## v0.1.0-beta.2 — 2026-03-03

### Fixed
- `AssetType` enum now deserializes correctly from JSON strings (e.g. `"Equity"`) via `JsonStringEnumConverter`
- Updated connection string template to use OS username instead of `postgres` (Homebrew PostgreSQL default)

### Added
- **Serilog** file logging: daily rolling log files written to `logs/api-YYYYMMDD.log`, retained for 30 days
- `GET /api/info` endpoint exposing API name, version, environment and timestamp

---

## v0.1.0-beta.1 — 2026-03-03

### Added
- Initial project scaffold: .NET 10 Web API with controller-based routing
- **Models**: `Security` (Equity/Bond/Forex), `EodPrice`, `WatchlistItem`, `AssetType` enum
- **Database**: PostgreSQL via EF Core + Npgsql; auto-migration on startup in Development
- **FinnhubService**: live quote fetch and EOD candle fetch (daily resolution)
- **Endpoints**:
  - `GET/POST/DELETE /api/securities` — manage tracked securities
  - `GET/POST/DELETE /api/watchlist` — manage watchlist with live quotes
  - `GET /api/prices/{id}` — retrieve stored EOD prices
  - `POST /api/prices/{id}/fetch` — fetch and store EOD prices from Finnhub
- Swagger UI available in Development at `/swagger`
- CORS configured for local frontend dev (`localhost:5173`, `localhost:5174`)
- `.gitignore` excludes `bin/`, `obj/`, `logs/`, `appsettings.Development.json`
