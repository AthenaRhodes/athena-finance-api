namespace AthenaFinance.Api.Services;

/// <summary>
/// Unified record returned by any provider's search.
/// </summary>
public record ProviderSearchResult(
    string ProviderId,
    string ProviderSymbol,   // symbol in THIS provider's notation (e.g. "MC.PA" for Yahoo)
    string DisplaySymbol,
    string Description,
    string Type,
    string Exchange);

public record ProviderProfile(
    string Name,
    string Exchange,
    string Industry,
    string Currency,
    string? Logo,
    string? WebUrl,
    decimal? MarketCapMillions);

public record ProviderQuote(
    decimal CurrentPrice,
    decimal? PercentChange,
    decimal? PreviousClose);

public record ProviderEodCandle(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

/// <summary>
/// Contract for a market data provider (Finnhub, Yahoo Finance, Twelve Data, etc.).
/// All implementations must handle failures gracefully and return null/empty on error.
/// </summary>
public interface IMarketDataProvider
{
    string ProviderId { get; }
    int Priority { get; }  // lower = higher priority; Finnhub=1, Yahoo=2

    Task<IList<ProviderSearchResult>> SearchAsync(string query);
    Task<ProviderProfile?> GetProfileAsync(string providerSymbol);
    Task<ProviderQuote?> GetQuoteAsync(string providerSymbol);
    Task<decimal?> GetYtdReturnAsync(string providerSymbol);
    Task<IList<ProviderEodCandle>> GetEodPricesAsync(string providerSymbol, DateTime from, DateTime to);
}
