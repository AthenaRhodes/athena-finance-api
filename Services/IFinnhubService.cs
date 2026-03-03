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

public interface IFinnhubService
{
    Task<FinnhubQuote?> GetQuoteAsync(string symbol);
    Task<IList<FinnhubCandle>> GetEodPricesAsync(string symbol, DateTime from, DateTime to);
}
