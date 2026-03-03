using AthenaFinance.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Security> Securities { get; set; }
    public DbSet<EodPrice> EodPrices { get; set; }
    public DbSet<WatchlistItem> WatchlistItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Security>(e =>
        {
            e.HasIndex(s => s.Symbol).IsUnique();
            e.Property(s => s.AssetType).HasConversion<string>();
            e.Property(s => s.MarketZone).HasConversion<string>();
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
    }
}
