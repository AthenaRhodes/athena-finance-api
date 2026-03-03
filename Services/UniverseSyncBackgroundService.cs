using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Services;

/// <summary>
/// Daily job — after US market close, fetches the full Polygon.io bulk snapshot
/// and stores EOD prices for ALL US securities in the universe.
/// Also syncs the Securities master list from Polygon's ticker reference.
/// Runs once daily at 22:30 UTC (after NYSE close + buffer).
/// </summary>
public class UniverseSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<UniverseSyncBackgroundService> logger) : BackgroundService
{
    private static readonly TimeOnly RunTime = new(22, 30); // UTC
    private DateOnly _lastRun = DateOnly.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Universe sync background service started");

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(30));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var nowUtc = DateTime.UtcNow;
            var todayUtc = DateOnly.FromDateTime(nowUtc);
            var currentTime = TimeOnly.FromDateTime(nowUtc);

            // Skip weekends
            if (nowUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;
            // Only run after market close
            if (currentTime < RunTime) continue;
            // Only run once per day
            if (_lastRun == todayUtc) continue;

            await SyncUniverseAsync(todayUtc, stoppingToken);
            _lastRun = todayUtc;
        }
    }

    private async Task SyncUniverseAsync(DateOnly date, CancellationToken ct)
    {
        logger.LogInformation("Universe sync starting for {Date}", date);
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var polygon = scope.ServiceProvider.GetRequiredService<IPolygonService>();
        var priceRepo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        try
        {
            // 1 — Fetch full snapshot (one API call)
            var snapshots = await polygon.GetAllSnapshotsAsync();
            if (snapshots.Count == 0)
            {
                logger.LogWarning("Universe sync: no snapshots returned from Polygon");
                return;
            }

            // 2 — Build lookup of existing securities by symbol
            var existingSymbols = await db.Securities
                .Where(s => s.AssetType == AssetType.Equity)
                .Select(s => new { s.Id, s.Symbol })
                .ToDictionaryAsync(s => s.Symbol, s => s.Id, ct);

            // 3 — Upsert securities not yet in DB (name resolved lazily — symbol only for now)
            var newSymbols = snapshots
                .Where(s => !existingSymbols.ContainsKey(s.Ticker) && !string.IsNullOrEmpty(s.Ticker))
                .Select(s => new Security
                {
                    Symbol = s.Ticker,
                    Name = s.Ticker, // Will be enriched by ticker detail sync separately
                    AssetType = AssetType.Equity,
                    MarketZone = MarketZone.US,
                    Currency = "USD"
                }).ToList();

            if (newSymbols.Count > 0)
            {
                db.Securities.AddRange(newSymbols);
                await db.SaveChangesAsync(ct);
                foreach (var s in newSymbols)
                    existingSymbols[s.Symbol] = s.Id;
                logger.LogInformation("Universe sync: {Count} new securities added", newSymbols.Count);
            }

            // 4 — Upsert EOD prices for all snapshots
            var pricesBatch = snapshots
                .Where(s => existingSymbols.ContainsKey(s.Ticker) && s.Close > 0)
                .Select(s => (
                    SecurityId: existingSymbols[s.Ticker],
                    Price: new EodPrice
                    {
                        Date = date,
                        Open = s.Open,
                        High = s.High,
                        Low = s.Low,
                        Close = s.Close,
                        Volume = s.Volume,
                        MarketCapMillions = s.MarketCap
                    }))
                .ToList();

            // Process in batches of 500 to avoid memory pressure
            foreach (var batch in pricesBatch.Chunk(500))
            {
                foreach (var (securityId, price) in batch)
                    await priceRepo.UpsertAsync(securityId, [price]);
            }

            logger.LogInformation("Universe sync complete: {Count} EOD prices stored for {Date}",
                pricesBatch.Count, date);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Universe sync failed for {Date}", date);
        }
    }
}
