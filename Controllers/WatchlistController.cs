using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WatchlistController(AppDbContext db, IFinnhubService finnhub) : ControllerBase
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
            var quote = await finnhub.GetQuoteAsync(item.Security.Symbol);
            var isEquity = item.Security.AssetType == Models.AssetType.Equity;
            var profile = isEquity ? await finnhub.GetProfileAsync(item.Security.Symbol) : null;
            var metrics = isEquity ? await finnhub.GetMetricsAsync(item.Security.Symbol) : null;
            result.Add(new
            {
                item.Id,
                item.Security.Symbol,
                item.Security.Name,
                item.Security.AssetType,
                item.Security.Currency,
                item.AddedAt,
                Quote = quote,
                MarketCapMillions = profile?.MarketCapMillions,
                Industry = profile?.Industry,
                Logo = profile?.Logo,
                Metrics = metrics
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
