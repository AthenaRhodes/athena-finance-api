using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Repositories;

public class SecurityRepository(AppDbContext db) : ISecurityRepository
{
    // Always include PriceSource so EffectiveSourceCode is available without extra queries
    private IQueryable<Security> WithProvider => db.Securities.Include(s => s.PriceSource);

    public Task<IList<Security>> GetAllAsync() =>
        WithProvider.OrderBy(s => s.Symbol).ToListAsync().ContinueWith(t => (IList<Security>)t.Result);

    public Task<Security?> GetByIdAsync(int id) =>
        WithProvider.FirstOrDefaultAsync(s => s.Id == id)!;

    public Task<Security?> GetBySymbolAsync(string symbol) =>
        WithProvider.FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpperInvariant());

    public async Task<Security> CreateAsync(Security security)
    {
        security.Symbol = security.Symbol.ToUpperInvariant();
        db.Securities.Add(security);
        await db.SaveChangesAsync();
        // Reload with PriceSource nav populated
        return (await WithProvider.FirstAsync(s => s.Id == security.Id));
    }

    public async Task DeleteAsync(int id)
    {
        var security = await db.Securities.FindAsync(id);
        if (security is not null)
        {
            db.Securities.Remove(security);
            await db.SaveChangesAsync();
        }
    }
}
