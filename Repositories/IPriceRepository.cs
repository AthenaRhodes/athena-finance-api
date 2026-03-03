using AthenaFinance.Api.Models;

namespace AthenaFinance.Api.Repositories;

public interface IPriceRepository
{
    Task<IList<EodPrice>> GetPricesAsync(int securityId, DateOnly from, DateOnly to);
    Task<EodPrice?> GetLatestAsync(int securityId);
    Task UpsertAsync(int securityId, IEnumerable<EodPrice> prices);
}
