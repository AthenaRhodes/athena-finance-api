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

public record FinnhubProfile(
    string Name,
    string Exchange,
    string Industry,
    string Currency,
    string Logo,
    string WebUrl,
    decimal MarketCapMillions
);

public interface IFinnhubService
{
    Task<FinnhubQuote?> GetQuoteAsync(string symbol);
    Task<FinnhubProfile?> GetProfileAsync(string symbol);
    Task<IList<FinnhubCandle>> GetEodPricesAsync(string symbol, DateTime from, DateTime to);
}
