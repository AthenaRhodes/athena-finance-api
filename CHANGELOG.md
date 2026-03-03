# Changelog — athena-finance-api

> Append-only. Each version is documented below in reverse chronological order.

---

## v0.1.0 — 2026-03-04

First stable release. Promotes v0.1.0-beta.12 to production-ready MVP.

### Summary
- .NET 10 Web API with clean architecture (Controllers, Services, Repositories)
- PostgreSQL via EF Core + Npgsql with auto-migration on startup (dev)
- Three data providers: Finnhub (equities), Frankfurter.app (forex), Polygon.io (OHLCV backfill)
- Full EOD price storage with market zone-aware nightly background service
- Strict live vs EOD data separation
- Serilog file logging with daily rolling files
- Swagger UI in development
- Semantic versioning + tagged releases

### Endpoints
- `GET/POST/DELETE /api/securities` — manage securities
- `GET /api/search?q=` — live symbol/company search via Finnhub
- `GET/POST/DELETE /api/watchlist` — manage watchlist with live quotes
- `GET /api/prices/{id}` — retrieve stored EOD prices
- `POST /api/prices/{id}/fetch` — fetch and store EOD prices
- `GET /api/info` — API name, version, environment
- `POST /api/admin/backfill?date=` — manual EOD backfill via Polygon + Finnhub
- `POST /api/admin/sync-universe` — full US universe sync (requires Polygon paid plan)

### Models
- `Security`: Symbol, Name, AssetType (Equity/Bond/Forex), MarketZone (US/EU/ASIA/FX), Exchange, Currency
- `EodPrice`: Date, Open, High, Low, Close, Volume, MarketCapMillions
- `WatchlistItem`: SecurityId, AddedAt

### Background services
- `EodPriceBackgroundService` — nightly EOD capture per market zone (watchlist securities)
- `UniverseSyncBackgroundService` — nightly full universe sync via Polygon (requires paid plan)

### Data providers
| Provider | Usage | Cost |
|---|---|---|
| Finnhub | Live quotes, search, metrics, equity profiles | Free |
| Frankfurter.app | Forex rates (ECB official), historical | Free, no key |
| Polygon.io | OHLCV backfill per ticker, ticker reference | Free tier |

---

## Beta history

### v0.1.0-beta.12 — 2026-03-04
- Backfill uses Polygon OHLCV — real open/high/low/close/volume; Finnhub as fallback

### v0.1.0-beta.11 — 2026-03-04
- Polygon.io integration: bulk snapshot, daily OHLCV, ticker details, universe sync service
- `POST /api/admin/sync-universe` endpoint (requires Polygon paid plan)

### v0.1.0-beta.10 — 2026-03-04
- Forex YTD % calculated from Frankfurter (Jan 2 vs today)

### v0.1.0-beta.9 — 2026-03-04
- Forex routing fixed via Frankfurter.app — resolves 403 on Finnhub forex quotes
- `IForexService.ParseSymbol` — parses `OANDA:EUR_USD` → `("EUR", "USD")`

### v0.1.0-beta.8 — 2026-03-04
- `POST /api/admin/backfill` endpoint — manual EOD backfill for watchlisted securities

### v0.1.0-beta.7 — 2026-03-04
- `MarketZone` enum on Security (US/EU/ASIA/FX)
- `EodPriceBackgroundService` — nightly EOD writes per zone
- Watchlist response split into `live` (Day% only) and `eod` (price, high, low, marketCap, date)

### v0.1.0-beta.6 — 2026-03-03
- `MarketCapMillions` added to EodPrices table
- Watchlist table streamlined: Symbol, Name, Industry, Price, Day%, YTD%, MktCap

### v0.1.0-beta.5 — 2026-03-03
- YTD return and 52W high/low via Finnhub `/stock/metric`

### v0.1.0-beta.4 — 2026-03-03
- Symbol search endpoint `GET /api/search?q=`
- Auto-resolve security name from Finnhub profile
- `CreateSecurityRequest.Name` now optional

### v0.1.0-beta.3 — 2026-03-03
- Market cap, industry, company logo via Finnhub `/stock/profile2`

### v0.1.0-beta.2 — 2026-03-03
- `JsonStringEnumConverter` — AssetType deserializes from string
- Serilog file logging: `logs/api-YYYYMMDD.log`, 30-day retention
- `GET /api/info` version endpoint

### v0.1.0-beta.1 — 2026-03-03
- Initial scaffold: .NET 10 Web API, EF Core, PostgreSQL, Finnhub, Swagger
