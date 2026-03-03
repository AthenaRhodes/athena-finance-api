using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Repositories;

public class PriceRepository(AppDbContext db) : IPriceRepository
{
    public Task<IList<EodPrice>> GetPricesAsync(int securityId, DateOnly from, DateOnly to) =>
        db.EodPrices
            .Where(p => p.SecurityId == securityId && p.Date >= from && p.Date <= to)
            .OrderBy(p => p.Date)
            .ToListAsync()
            .ContinueWith(t => (IList<EodPrice>)t.Result);

    public async Task UpsertAsync(int securityId, IEnumerable<EodPrice> prices)
    {
        foreach (var price in prices)
        {
            var existing = await db.EodPrices
                .FirstOrDefaultAsync(p => p.SecurityId == securityId && p.Date == price.Date);

            if (existing is null)
            {
                price.SecurityId = securityId;
                db.EodPrices.Add(price);
            }
            else
            {
                existing.Open = price.Open;
                existing.High = price.High;
                existing.Low = price.Low;
                existing.Close = price.Close;
                existing.Volume = price.Volume;
                existing.FetchedAt = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync();
    }
}
