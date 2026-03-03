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
            // Live: only Day% (intraday, refreshes every 30s on frontend)
            var quote = await finnhub.GetQuoteAsync(item.Security.Symbol);

            // EOD: price, market cap from DB
            var eod = await priceRepo.GetLatestAsync(item.SecurityId);

            // Profile + metrics for non-price display fields
            var isEquity = item.Security.AssetType == AssetType.Equity;
            var profile = isEquity ? await finnhub.GetProfileAsync(item.Security.Symbol) : null;
            var metrics = isEquity ? await finnhub.GetMetricsAsync(item.Security.Symbol) : null;

            result.Add(new
            {
                item.Id,
                item.Security.Symbol,
                item.Security.Name,
                item.Security.AssetType,
                item.Security.MarketZone,
                item.Security.Currency,
                item.AddedAt,
                Industry = profile?.Industry,
                Logo = profile?.Logo,
                // Live intraday — Day% only
                Live = quote is null ? null : new
                {
                    DayChangePercent = quote.PercentChange,
                },
                // EOD from DB — price, market cap, date
                Eod = eod is null ? null : new
                {
                    Date = eod.Date,
                    Close = eod.Close,
                    High = eod.High,
                    Low = eod.Low,
                    MarketCapMillions = eod.MarketCapMillions
                },
                // Performance metrics from Finnhub (based on EOD data on their side)
                YtdReturn = metrics?.YtdReturn,
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
