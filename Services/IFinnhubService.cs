namespace AthenaFinance.Api.Services;

public record FinnhubQuote(
    decimal CurrentPrice,
    decimal Change,
    decimal PercentChange,
    decimal High,
    decimal Low,
    decimal Open,
    decimal PreviousClose
);

public record FinnhubCandle(
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume
);

public record FinnhubMetrics(
    decimal? YtdReturn,
    decimal? Return5D,
    decimal? Return13W,
    decimal? Return26W,
    decimal? Return52W,
    decimal? High52W,
    decimal? Low52W,
    string? High52WDate,
    string? Low52WDate
);

public record FinnhubProfile(
    string Name,
    string Exchange,
    string Industry,
    string Currency,
    string Logo,
    string WebUrl,
    decimal MarketCapMillions
);

public record FinnhubSearchResult(
    string Symbol,
    string DisplaySymbol,
    string Description,
    string Type
);

public interface IFinnhubService
{
    Task<FinnhubQuote?> GetQuoteAsync(string symbol);
    Task<FinnhubProfile?> GetProfileAsync(string symbol);
    Task<FinnhubMetrics?> GetMetricsAsync(string symbol);
    Task<IList<FinnhubSearchResult>> SearchAsync(string query);
    Task<IList<FinnhubCandle>> GetEodPricesAsync(string symbol, DateTime from, DateTime to);
}
