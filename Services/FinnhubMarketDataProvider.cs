namespace AthenaFinance.Api.Services;

/// <summary>
/// Wraps the existing FinnhubService to implement IMarketDataProvider.
/// Priority 1 — best for US/CAD stocks. Limited free-tier coverage for EU/ASIA.
/// </summary>
public class FinnhubMarketDataProvider(IFinnhubService finnhub) : IMarketDataProvider
{
    public string ProviderId => "finnhub";
    public int Priority => 1;

    public async Task<IList<ProviderSearchResult>> SearchAsync(string query)
    {
        var results = await finnhub.SearchAsync(query);
        return results
            .Where(r => r.Type == "Common Stock" || r.Type == "EQS" || r.Type == "")
            .Select(r => new ProviderSearchResult(
                ProviderId: "finnhub",
                ProviderSymbol: r.Symbol,
                DisplaySymbol: r.DisplaySymbol,
                Description: r.Description,
                Type: r.Type,
                Exchange: string.Empty))
            .ToList();
    }

    public async Task<ProviderProfile?> GetProfileAsync(string providerSymbol)
    {
        var profile = await finnhub.GetProfileAsync(providerSymbol);
        if (profile is null) return null;
        return new ProviderProfile(
            Name: profile.Name,
            Exchange: profile.Exchange,
            Industry: profile.Industry,
            Currency: profile.Currency,
            Logo: profile.Logo,
            WebUrl: profile.WebUrl,
            MarketCapMillions: profile.MarketCapMillions);
    }

    public async Task<ProviderQuote?> GetQuoteAsync(string providerSymbol)
    {
        var q = await finnhub.GetQuoteAsync(providerSymbol);
        if (q is null) return null;
        return new ProviderQuote(q.CurrentPrice, q.PercentChange, q.PreviousClose);
    }

    public async Task<decimal?> GetYtdReturnAsync(string providerSymbol)
    {
        var m = await finnhub.GetMetricsAsync(providerSymbol);
        return m?.YtdReturn;
    }

    public async Task<IList<ProviderEodCandle>> GetEodPricesAsync(string providerSymbol, DateTime from, DateTime to)
    {
        var candles = await finnhub.GetEodPricesAsync(providerSymbol, from, to);
        return candles
            .Select(c => new ProviderEodCandle(c.Date, c.Open, c.High, c.Low, c.Close, c.Volume))
            .ToList();
    }
}
