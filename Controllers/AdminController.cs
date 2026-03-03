using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController(
    AppDbContext db,
    IFinnhubService finnhub,
    IForexService forex,
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
                    close = rate.Rate;
                }
                else
                {
                    var quote = await finnhub.GetQuoteAsync(security.Symbol);
                    if (quote is null)
                    {
                        results.Add(new { security.Symbol, status = "skipped", reason = "no quote" });
                        continue;
                    }
                    close = quote.PreviousClose;
                    var isEquity = security.AssetType == AssetType.Equity;
                    var profile = isEquity ? await finnhub.GetProfileAsync(security.Symbol) : null;
                    marketCap = profile?.MarketCapMillions;
                }

                await priceRepo.UpsertAsync(security.Id, [new EodPrice
                {
                    Date = targetDate,
                    Open = close,
                    High = close,
                    Low = close,
                    Close = close,
                    Volume = 0,
                    MarketCapMillions = marketCap
                }]);

                logger.LogInformation("Backfill stored: {Symbol} {Date} close={Close}",
                    security.Symbol, targetDate, close);

                results.Add(new { security.Symbol, status = "ok", date = targetDate, close });

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
}
