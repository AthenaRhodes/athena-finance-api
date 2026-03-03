# Changelog — athena-finance-api

> Append-only. Each beta version is documented below in reverse chronological order.

---

## v0.1.0-beta.7 — 2026-03-04

### Added
- `MarketZone` enum: `US`, `EU`, `ASIA`, `FX` — stored as string in DB
- `MarketZone` field on `Security` — defaults to `US` for equities/bonds, `FX` for forex
- `EodPriceBackgroundService` — runs hourly; after each zone's market close, fetches and stores settled EOD price, high, low, market cap for all securities in that zone
- `IPriceRepository.GetLatestAsync` — returns most recent EOD record for a security

### Changed
- Watchlist `GET` no longer auto-upserts on refresh — EOD writes are now exclusively managed by the background service
- Watchlist response restructured: `live` (Day% only) and `eod` (close, high, low, marketCap, date) are separate objects
- `CreateSecurityRequest` accepts optional `MarketZone` (auto-defaults if omitted)

### Migrations
- `AddMarketZoneToSecurity`

---

## v0.1.0-beta.6 — 2026-03-03

### Added
- `MarketCapMillions` column added to `EodPrices` table (migration: `AddMarketCapToEodPrice`)
- Watchlist `GET` now auto-upserts today's daily snapshot (price, high, low, market cap) on every refresh — no manual trigger needed

### Changed
- `EodPrice` model: `MarketCapMillions` added as nullable decimal

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

---

## v0.1.0-beta.9 — 2026-03-04

### Added
- `IForexService` / `FrankfurterService` — forex rates via Frankfurter.app (free, no API key, ECB official daily rates)
- Forex Day% calculated as today vs previous business day rate
- Forex historical rates supported (used in backfill and background service)
- `IForexService.ParseSymbol` — parses `OANDA:EUR_USD` → `("EUR", "USD")`

### Fixed
- Forex securities no longer crash with 403 — routed to Frankfurter instead of Finnhub `/quote`

### Changed
- `WatchlistController`, `EodPriceBackgroundService`, `AdminController` all branch on `AssetType.Forex` to use `IForexService`

---

## v0.1.0-beta.8 — 2026-03-04

### Added
- `POST /api/admin/backfill?date=YYYY-MM-DD` — manually backfill EOD prices for all watchlisted securities
- Defaults to yesterday if no date provided; rejects weekends
- Uses `previousClose` from Finnhub quote (free tier limitation — full historical OHLC requires paid plan)
- High/Low stored as 0 when historical data unavailable (clearly flagged in logs)
