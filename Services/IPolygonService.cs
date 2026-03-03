namespace AthenaFinance.Api.Services;

public record PolygonSnapshot(
    string Ticker,
    decimal Close,
    decimal Open,
    decimal High,
    decimal Low,
    long Volume,
    decimal DayChangePercent,
    decimal? MarketCap
);

public record PolygonDailyOhlc(
    string Ticker,
    DateOnly Date,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume
);

public record PolygonTickerDetail(
    string Ticker,
    string Name,
    string Market,
    string Locale,
    string Type,
    string CurrencyName,
    decimal? MarketCap,
    string? Description
);

public interface IPolygonService
{
    /// <summary>
    /// Bulk snapshot — returns current day data for all US stocks in one call.
    /// Free tier: 15-min delayed data. Perfect for EOD use.
    /// </summary>
    Task<IList<PolygonSnapshot>> GetAllSnapshotsAsync();

    /// <summary>Historical daily OHLCV for a specific ticker and date.</summary>
    Task<PolygonDailyOhlc?> GetDailyOhlcAsync(string ticker, DateOnly date);

    /// <summary>Ticker details (name, type, market cap etc.).</summary>
    Task<PolygonTickerDetail?> GetTickerDetailAsync(string ticker);

    /// <summary>Full list of US stock tickers with basic metadata.</summary>
    Task<IList<PolygonTickerDetail>> GetAllTickersAsync(string? type = null);
}
