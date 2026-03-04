using System.ComponentModel.DataAnnotations.Schema;

namespace AthenaFinance.Api.Models;

public class Security
{
    public int      Id         { get; set; }
    public string   Symbol     { get; set; } = string.Empty;
    public string   Name       { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public string   Exchange   { get; set; } = string.Empty;
    public string   Currency   { get; set; } = "USD";
    public MarketZone MarketZone { get; set; } = MarketZone.US;
    public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;

    // ── Price source ────────────────────────────────────────────────────────

    /// <summary>FK → PriceProviders. Which provider to use for price refresh.</summary>
    public int? PriceSourceId { get; set; }

    /// <summary>Navigation property to the provider record.</summary>
    public PriceProvider? PriceSource { get; set; }

    /// <summary>
    /// Provider-specific symbol when it differs from Symbol
    /// (e.g. Yahoo Finance uses "MC.PA" for LVMH while Symbol might be "MC").
    /// Null = use Symbol as-is.
    /// </summary>
    public string? PriceSourceSymbol { get; set; }

    // ── Computed helpers (not mapped) ───────────────────────────────────────

    /// <summary>Symbol to pass to the provider when fetching prices.</summary>
    [NotMapped]
    public string EffectiveSourceSymbol => PriceSourceSymbol ?? Symbol;

    /// <summary>Provider code (e.g. "finnhub") resolved via nav property; falls back to "finnhub".</summary>
    [NotMapped]
    public string EffectiveSourceCode => PriceSource?.Code ?? "finnhub";

    // ── Navigation ──────────────────────────────────────────────────────────

    public ICollection<EodPrice>      Prices        { get; set; } = new List<EodPrice>();
    public ICollection<WatchlistItem> WatchlistItems { get; set; } = new List<WatchlistItem>();
}
