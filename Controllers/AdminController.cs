using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    AppDbContext db,
    IFinnhubService finnhub,
    IForexService forex,
    IPolygonService polygon,
    IPriceRepository priceRepo,
    ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>
    /// Backfill EOD prices for all watchlisted securities for a given date.
    /// Defaults to yesterday. Uses previousClose from the Finnhub quote endpoint
    /// (free tier limitation — full historical OHLC requires a paid plan).
    /// </summary>
    [HttpPost("backfill")]
    public async Task<IActionResult> Backfill([FromQuery] DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        // Skip weekends
        var dow = targetDate.DayOfWeek;
        if (dow is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return BadRequest(new { message = $"{targetDate} is a weekend — no market data." });

        var securities = await db.WatchlistItems
            .Include(w => w.Security)
            .Select(w => w.Security)
            .Distinct()
            .ToListAsync();

        if (securities.Count == 0)
            return Ok(new { message = "No securities in watchlist.", stored = 0 });

        logger.LogInformation("Backfill requested for {Date} — {Count} securities", targetDate, securities.Count);

        var results = new List<object>();

        foreach (var security in securities)
        {
            try
            {
                decimal close;
                decimal? marketCap = null;

                decimal open = 0, high = 0, low = 0, volume = 0;

                if (security.AssetType == AssetType.Forex)
                {
                    var parsed = IForexService.ParseSymbol(security.Symbol);
                    if (parsed is null)
                    {
                        results.Add(new { security.Symbol, status = "skipped", reason = "invalid forex symbol format" });
                        continue;
                    }
                    var rate = await forex.GetHistoricalRateAsync(parsed.Value.Base, parsed.Value.Quote, targetDate);
                    if (rate is null)
                    {
                        results.Add(new { security.Symbol, status = "skipped", reason = "no rate from Frankfurter" });
                        continue;
                    }
                    close = open = high = low = rate.Rate;
                }
                else
                {
                    // Use Polygon for proper OHLCV (better than Finnhub previousClose only)
                    var ohlc = await polygon.GetDailyOhlcAsync(security.Symbol, targetDate);
                    if (ohlc is not null)
                    {
                        close = ohlc.Close; open = ohlc.Open;
                        high = ohlc.High; low = ohlc.Low;
                        volume = ohlc.Volume;
                    }
                    else
                    {
                        // Fallback to Finnhub previousClose
                        var quote = await finnhub.GetQuoteAsync(security.Symbol);
                        if (quote is null)
                        {
                            results.Add(new { security.Symbol, status = "skipped", reason = "no data from Polygon or Finnhub" });
                            continue;
                        }
                        close = open = high = low = quote.PreviousClose;
                    }

                    // Market cap from Polygon reference (more precise than Finnhub)
                    var detail = await polygon.GetTickerDetailAsync(security.Symbol);
                    marketCap = detail?.MarketCap;
                }

                await priceRepo.UpsertAsync(security.Id, [new EodPrice
                {
                    Date = targetDate,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = (long)volume,
                    MarketCapMillions = marketCap
                }]);

                logger.LogInformation("Backfill stored: {Symbol} {Date} O={Open} H={High} L={Low} C={Close}",
                    security.Symbol, targetDate, open, high, low, close);

                results.Add(new { security.Symbol, status = "ok", date = targetDate, open, high, low, close, marketCap });

                await Task.Delay(300); // rate limit courtesy
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Backfill failed for {Symbol}", security.Symbol);
                results.Add(new { security.Symbol, status = "error", reason = ex.Message });
            }
        }

        return Ok(new { date = targetDate, stored = results.Count(r => r.ToString()!.Contains("ok")), results });
    }

    /// <summary>
    /// Trigger a full Polygon universe sync for today (or a specific date).
    /// Fetches all US stock EOD prices in one bulk Polygon call.
    /// </summary>
    [HttpPost("sync-universe")]
    public async Task<IActionResult> SyncUniverse([FromQuery] DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var sw = Stopwatch.StartNew();

        logger.LogInformation("Manual universe sync triggered for {Date}", targetDate);

        var snapshots = await polygon.GetAllSnapshotsAsync();
        if (snapshots.Count == 0)
            return StatusCode(503, new { message = "No data returned from Polygon. Check API key or try later." });

        // Upsert securities
        var existingSymbols = await db.Securities
            .Where(s => s.AssetType == AssetType.Equity)
            .Select(s => new { s.Id, s.Symbol })
            .ToDictionaryAsync(s => s.Symbol, s => s.Id);

        var newSecurities = snapshots
            .Where(s => !existingSymbols.ContainsKey(s.Ticker) && !string.IsNullOrEmpty(s.Ticker))
            .Select(s => new Security
            {
                Symbol = s.Ticker,
                Name = s.Ticker,
                AssetType = AssetType.Equity,
                MarketZone = MarketZone.US,
                Currency = "USD"
            }).ToList();

        if (newSecurities.Count > 0)
        {
            db.Securities.AddRange(newSecurities);
            await db.SaveChangesAsync();
            foreach (var s in newSecurities)
                existingSymbols[s.Symbol] = s.Id;
        }

        // Upsert EOD prices
        var priceCount = 0;
        foreach (var s in snapshots.Where(s => existingSymbols.ContainsKey(s.Ticker) && s.Close > 0))
        {
            await priceRepo.UpsertAsync(existingSymbols[s.Ticker], [new EodPrice
            {
                Date = targetDate,
                Open = s.Open,
                High = s.High,
                Low = s.Low,
                Close = s.Close,
                Volume = s.Volume,
                MarketCapMillions = s.MarketCap
            }]);
            priceCount++;
        }

        sw.Stop();
        return Ok(new
        {
            date = targetDate,
            snapshotsFetched = snapshots.Count,
            newSecurities = newSecurities.Count,
            pricesStored = priceCount,
            elapsedMs = sw.ElapsedMilliseconds
        });
    }
}
