using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Repositories;

public class SecurityRepository(AppDbContext db) : ISecurityRepository
{
    public Task<IList<Security>> GetAllAsync() =>
        db.Securities.OrderBy(s => s.Symbol).ToListAsync().ContinueWith(t => (IList<Security>)t.Result);

    public Task<Security?> GetByIdAsync(int id) =>
        db.Securities.FindAsync(id).AsTask()!;

    public Task<Security?> GetBySymbolAsync(string symbol) =>
        db.Securities.FirstOrDefaultAsync(s => s.Symbol == symbol.ToUpperInvariant());

    public async Task<Security> CreateAsync(Security security)
    {
        security.Symbol = security.Symbol.ToUpperInvariant();
        db.Securities.Add(security);
        await db.SaveChangesAsync();
        return security;
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
