namespace AthenaFinance.Api.Services;

/// <summary>
/// Chains all registered IMarketDataProvider implementations in priority order.
/// - Search: queries all in parallel, deduplicates by ProviderSymbol
/// - Profile/Quote/EOD: tries primary provider first, falls back to others
/// </summary>
public class MarketDataAggregator(
    IEnumerable<IMarketDataProvider> providers,
    ILogger<MarketDataAggregator> logger)
{
    private IEnumerable<IMarketDataProvider> Ordered => providers.OrderBy(p => p.Priority);

    // ─── Search ─────────────────────────────────────────────────────────────

    public async Task<IList<ProviderSearchResult>> SearchAsync(string query)
    {
        var tasks = Ordered.Select(p => SafeSearch(p, query));
        var all   = await Task.WhenAll(tasks);

        // Deduplicate by ProviderSymbol — keep first occurrence (highest priority provider)
        return all
            .SelectMany(r => r)
            .GroupBy(r => r.ProviderSymbol)
            .Select(g => g.First())
            .ToList();
    }

    // ─── Profile (with fallback chain) ──────────────────────────────────────

    /// <summary>
    /// Resolves a profile, trying the preferred provider first then falling back.
    /// Returns (profile, resolvedProviderId, resolvedSymbol).
    /// </summary>
    public async Task<(ProviderProfile? Profile, string? ProviderId, string? ProviderSymbol)> ResolveProfileAsync(
        string symbol,
        string? preferredProviderId    = null,
        string? preferredProviderSymbol = null)
    {
        // Try the preferred provider first (e.g. "yahoo" with "MC.PA")
        if (preferredProviderId is not null)
        {
            var preferred = Ordered.FirstOrDefault(p => p.ProviderId == preferredProviderId);
            if (preferred is not null)
            {
                var sym     = preferredProviderSymbol ?? symbol;
                var profile = await SafeProfile(preferred, sym);
                if (profile is not null)
                    return (profile, preferredProviderId, sym);
            }
        }

        // Fallback: try remaining providers with the canonical symbol
        foreach (var provider in Ordered)
        {
            if (provider.ProviderId == preferredProviderId) continue; // already tried
            var profile = await SafeProfile(provider, symbol);
            if (profile is not null)
                return (profile, provider.ProviderId, symbol);
        }

        return (null, null, null);
    }

    // ─── Quote ──────────────────────────────────────────────────────────────

    public async Task<ProviderQuote?> GetQuoteAsync(string priceSourceId, string priceSourceSymbol)
    {
        var primary = Ordered.FirstOrDefault(p => p.ProviderId == priceSourceId);
        if (primary is not null)
        {
            var q = await SafeQuote(primary, priceSourceSymbol);
            if (q is not null) return q;
        }
        foreach (var p in Ordered.Where(p => p.ProviderId != priceSourceId))
        {
            var q = await SafeQuote(p, priceSourceSymbol);
            if (q is not null) return q;
        }
        return null;
    }

    // ─── YTD Return ─────────────────────────────────────────────────────────

    public async Task<decimal?> GetYtdReturnAsync(string priceSourceId, string priceSourceSymbol)
    {
        var primary = Ordered.FirstOrDefault(p => p.ProviderId == priceSourceId);
        if (primary is not null)
        {
            try
            {
                var ytd = await primary.GetYtdReturnAsync(priceSourceSymbol);
                if (ytd is not null) return ytd;
            }
            catch (Exception ex) { logger.LogWarning(ex, "[Aggregator] YTD failed for {Provider}", priceSourceId); }
        }
        foreach (var p in Ordered.Where(p => p.ProviderId != priceSourceId))
        {
            try
            {
                var ytd = await p.GetYtdReturnAsync(priceSourceSymbol);
                if (ytd is not null) return ytd;
            }
            catch { /* swallow, try next */ }
        }
        return null;
    }

    // ─── EOD Prices ─────────────────────────────────────────────────────────

    public async Task<IList<ProviderEodCandle>> GetEodPricesAsync(
        string priceSourceId, string priceSourceSymbol, DateTime from, DateTime to)
    {
        var primary = Ordered.FirstOrDefault(p => p.ProviderId == priceSourceId);
        if (primary is not null)
        {
            try
            {
                var c = await primary.GetEodPricesAsync(priceSourceSymbol, from, to);
                if (c.Count > 0) return c;
            }
            catch (Exception ex) { logger.LogWarning(ex, "[Aggregator] EOD failed for {Provider}", priceSourceId); }
        }
        foreach (var p in Ordered.Where(p => p.ProviderId != priceSourceId))
        {
            try
            {
                var c = await p.GetEodPricesAsync(priceSourceSymbol, from, to);
                if (c.Count > 0) return c;
            }
            catch { /* try next */ }
        }
        return [];
    }

    // ─── Private helpers ────────────────────────────────────────────────────

    private async Task<IList<ProviderSearchResult>> SafeSearch(IMarketDataProvider provider, string query)
    {
        try { return await provider.SearchAsync(query); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Aggregator] Search failed for provider '{Id}'", provider.ProviderId);
            return [];
        }
    }

    private async Task<ProviderProfile?> SafeProfile(IMarketDataProvider provider, string symbol)
    {
        try { return await provider.GetProfileAsync(symbol); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Aggregator] GetProfile failed for provider '{Id}' symbol '{Symbol}'", provider.ProviderId, symbol);
            return null;
        }
    }

    private async Task<ProviderQuote?> SafeQuote(IMarketDataProvider provider, string symbol)
    {
        try { return await provider.GetQuoteAsync(symbol); }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Aggregator] GetQuote failed for provider '{Id}' symbol '{Symbol}'", provider.ProviderId, symbol);
            return null;
        }
    }
}
