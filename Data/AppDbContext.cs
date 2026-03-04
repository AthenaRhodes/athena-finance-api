using AthenaFinance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Security>      Securities     { get; set; }
    public DbSet<EodPrice>      EodPrices      { get; set; }
    public DbSet<WatchlistItem> WatchlistItems { get; set; }
    public DbSet<PriceProvider> PriceProviders { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Security>(e =>
        {
            e.HasIndex(s => s.Symbol).IsUnique();
            e.Property(s => s.AssetType).HasConversion<string>();
            e.Property(s => s.MarketZone).HasConversion<string>();

            e.HasOne(s => s.PriceSource)
             .WithMany(p => p.Securities)
             .HasForeignKey(s => s.PriceSourceId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EodPrice>(e =>
        {
            e.HasIndex(p => new { p.SecurityId, p.Date }).IsUnique();
            e.Property(p => p.Open).HasPrecision(18, 6);
            e.Property(p => p.High).HasPrecision(18, 6);
            e.Property(p => p.Low).HasPrecision(18, 6);
            e.Property(p => p.Close).HasPrecision(18, 6);
            e.Property(p => p.MarketCapMillions).HasPrecision(24, 6);
        });

        modelBuilder.Entity<PriceProvider>(e =>
        {
            e.HasIndex(p => p.Code).IsUnique();
        });

        // ── Seed providers ──────────────────────────────────────────────────
        modelBuilder.Entity<PriceProvider>().HasData(
            new PriceProvider
            {
                Id            = 1,
                Code          = "finnhub",
                Name          = "Finnhub",
                Description   = "Real-time & EOD data for US/CAD stocks. Free tier: 60 req/min.",
                Priority      = 1,
                RequiresApiKey = true,
                BaseUrl       = "https://finnhub.io/api/v1/",
                IsActive      = true,
                Notes         = "Best coverage for US/NASDAQ/NYSE. Limited free-tier support for EU/ASIA."
            },
            new PriceProvider
            {
                Id            = 2,
                Code          = "yahoo",
                Name          = "Yahoo Finance",
                Description   = "Unofficial API. No key required. Global coverage (EU, ASIA, US).",
                Priority      = 2,
                RequiresApiKey = false,
                BaseUrl       = "https://query1.finance.yahoo.com/",
                IsActive      = true,
                Notes         = "Unofficial — no SLA. Symbols use exchange suffixes: MC.PA, SIE.DE, 7203.T, 0700.HK etc."
            },
            new PriceProvider
            {
                Id            = 3,
                Code          = "frankfurter",
                Name          = "Frankfurter (ECB)",
                Description   = "Official ECB exchange rates. Free, no key, major currency pairs only.",
                Priority      = 3,
                RequiresApiKey = false,
                BaseUrl       = "https://api.frankfurter.app/",
                IsActive      = true,
                Notes         = "Used exclusively for Forex. Only covers ECB-published pairs."
            },
            new PriceProvider
            {
                Id            = 4,
                Code          = "polygon",
                Name          = "Polygon.io",
                Description   = "US stock universe bulk snapshots + OHLCV. Free tier: 15-min delayed.",
                Priority      = 4,
                RequiresApiKey = true,
                BaseUrl       = "https://api.polygon.io/",
                IsActive      = true,
                Notes         = "Used by UniverseSyncBackgroundService for bulk US EOD snapshots."
            }
        );
    }
}
