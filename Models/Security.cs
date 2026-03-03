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

    public ICollection<EodPrice> Prices { get; set; } = new List<EodPrice>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
