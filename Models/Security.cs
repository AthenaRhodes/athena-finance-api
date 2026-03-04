namespace AthenaFinance.Api.Models;

public class Security
{
    public int Id { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string Exchange { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public MarketZone MarketZone { get; set; } = MarketZone.US;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Which market data provider to use for price refresh (e.g. "finnhub", "yahoo").</summary>
    public string? PriceSourceId { get; set; }

    /// <summary>
    /// The symbol in the provider's notation if different from Symbol (e.g. Yahoo uses "MC.PA" for LVMH).
    /// Null means use Symbol as-is.
    /// </summary>
    public string? PriceSourceSymbol { get; set; }

    /// <summary>Symbol to use when fetching prices from the stored provider.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string EffectiveSourceSymbol => PriceSourceSymbol ?? Symbol;

    public ICollection<EodPrice> Prices { get; set; } = new List<EodPrice>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
