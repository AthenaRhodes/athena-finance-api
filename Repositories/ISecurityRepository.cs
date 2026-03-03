using AthenaFinance.Api.Models;

namespace AthenaFinance.Api.Repositories;

public interface ISecurityRepository
{
    Task<IList<Security>> GetAllAsync();
    Task<Security?> GetByIdAsync(int id);
    Task<Security?> GetBySymbolAsync(string symbol);
    Task<Security> CreateAsync(Security security);
    Task DeleteAsync(int id);
}
