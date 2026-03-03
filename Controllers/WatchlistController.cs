using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WatchlistController(
    AppDbContext db,
    IFinnhubService finnhub,
    IForexService forex,
    IPriceRepository priceRepo) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetWatchlist()
    {
        var items = await db.WatchlistItems
            .Include(w => w.Security)
            .OrderBy(w => w.Security.Symbol)
            .ToListAsync();

        var result = new List<object>();

        foreach (var item in items)
        {
            var isEquity = item.Security.AssetType == AssetType.Equity;
            var isForex  = item.Security.AssetType == AssetType.Forex;

            // EOD from DB
            var eod = await priceRepo.GetLatestAsync(item.SecurityId);

            // Live Day% + profile/metrics
            decimal? dayChangePct = null;
            string? industry = null;
            string? logo = null;
            decimal? ytdReturn = null;

            if (isForex)
            {
                var parsed = IForexService.ParseSymbol(item.Security.Symbol);
                if (parsed is not null)
                {
                    var (rates, yesterday) = await forex.GetRatesAsync(parsed.Value.Base, parsed.Value.Quote);
                    if (rates is not null && yesterday is not null && yesterday.Rate != 0)
                        dayChangePct = Math.Round((rates.Rate - yesterday.Rate) / yesterday.Rate * 100, 4);
                    ytdReturn = await forex.GetYtdReturnAsync(parsed.Value.Base, parsed.Value.Quote);
                }
            }
            else
            {
                var quote = await finnhub.GetQuoteAsync(item.Security.Symbol);
                dayChangePct = quote?.PercentChange;

                if (isEquity)
                {
                    var profile = await finnhub.GetProfileAsync(item.Security.Symbol);
                    var metrics = await finnhub.GetMetricsAsync(item.Security.Symbol);
                    industry = profile?.Industry;
                    logo = profile?.Logo;
                    ytdReturn = metrics?.YtdReturn;
                }
            }

            result.Add(new
            {
                item.Id,
                item.Security.Symbol,
                item.Security.Name,
                item.Security.AssetType,
                item.Security.MarketZone,
                item.Security.Currency,
                item.AddedAt,
                Industry = industry,
                Logo = logo,
                Live = dayChangePct is null ? null : new { DayChangePercent = dayChangePct },
                Eod = eod is null ? null : new
                {
                    Date = eod.Date,
                    Close = eod.Close,
                    High = eod.High,
                    Low = eod.Low,
                    MarketCapMillions = eod.MarketCapMillions
                },
                YtdReturn = ytdReturn,
            });
        }

        return Ok(result);
    }

    [HttpPost("{securityId:int}")]
    public async Task<IActionResult> AddToWatchlist(int securityId)
    {
        var security = await db.Securities.FindAsync(securityId);
        if (security is null) return NotFound();

        var existing = await db.WatchlistItems.FirstOrDefaultAsync(w => w.SecurityId == securityId);
        if (existing is not null) return Conflict(new { message = "Already in watchlist." });

        var item = new WatchlistItem { SecurityId = securityId };
        db.WatchlistItems.Add(item);
        await db.SaveChangesAsync();
        return Ok(item);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveFromWatchlist(int id)
    {
        var item = await db.WatchlistItems.FindAsync(id);
        if (item is null) return NotFound();
        db.WatchlistItems.Remove(item);
        await db.SaveChangesAsync();
        return NoContent();
    }
}
