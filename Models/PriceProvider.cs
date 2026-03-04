namespace AthenaFinance.Api.Models;

/// <summary>
/// Registered market data provider. Drives the fallback chain and is stored
/// as a FK on Security so every price refresh always hits the right source.
/// </summary>
public class PriceProvider
{
    public int    Id            { get; set; }

    /// <summary>Matches IMarketDataProvider.ProviderId (e.g. "finnhub", "yahoo").</summary>
    public string Code          { get; set; } = string.Empty;

    public string Name          { get; set; } = string.Empty;
    public string? Description  { get; set; }

    /// <summary>Lower value = higher priority in the fallback chain.</summary>
    public int    Priority      { get; set; }

    public bool   RequiresApiKey { get; set; }
    public string? BaseUrl      { get; set; }

    /// <summary>Disabled providers are skipped by the aggregator.</summary>
    public bool   IsActive      { get; set; } = true;

    public string? Notes        { get; set; }

    // Navigation
    public ICollection<Security> Securities { get; set; } = new List<Security>();
}
