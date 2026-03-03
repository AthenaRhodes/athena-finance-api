namespace AthenaFinance.Api.Services;

public record ForexRate(
    string Base,
    string Quote,
    decimal Rate,
    DateOnly Date
);

public interface IForexService
{
    /// <summary>Returns today's ECB rate and calculates Day% vs yesterday.</summary>
    Task<(ForexRate? Today, ForexRate? Yesterday)> GetRatesAsync(string baseCcy, string quoteCcy);

    /// <summary>Returns the ECB rate for a specific date.</summary>
    Task<ForexRate?> GetHistoricalRateAsync(string baseCcy, string quoteCcy, DateOnly date);

    /// <summary>Returns YTD return % using the first available ECB rate of the current year.</summary>
    Task<decimal?> GetYtdReturnAsync(string baseCcy, string quoteCcy);

    /// <summary>Parses "OANDA:EUR_USD" → ("EUR", "USD"). Returns null if format invalid.</summary>
    static (string Base, string Quote)? ParseSymbol(string symbol)
    {
        // Format: EXCHANGE:BASE_QUOTE  e.g. OANDA:EUR_USD
        var parts = symbol.Contains(':') ? symbol.Split(':')[1] : symbol;
        var currencies = parts.Split('_');
        if (currencies.Length != 2) return null;
        return (currencies[0].ToUpperInvariant(), currencies[1].ToUpperInvariant());
    }
}
