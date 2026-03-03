using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Services;

/// <summary>
/// Runs hourly. After each market zone closes, fetches and stores the EOD
/// price (close, high, low) and market cap for all securities in that zone.
/// </summary>
public class EodPriceBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EodPriceBackgroundService> logger) : BackgroundService
{
    // UTC times after which we consider a zone's market closed for the day
    private static readonly Dictionary<MarketZone, TimeOnly> EodTimes = new()
    {
        { MarketZone.US,   new TimeOnly(21, 30) }, // NYSE/NASDAQ close ~21:00 UTC + buffer
        { MarketZone.EU,   new TimeOnly(18, 00) }, // LSE/Euronext/Xetra ~17:30 UTC + buffer
        { MarketZone.ASIA, new TimeOnly(09, 00) }, // TSE/HKEX ~08:00 UTC + buffer
        { MarketZone.FX,   new TimeOnly(23, 00) }, // End of NY FX session
    };

    // Track which zones we've already fetched for today
    private readonly Dictionary<MarketZone, DateOnly> _lastFetched = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EOD price background service started");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        // Run once on startup, then every hour
        await TryFetchEodAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryFetchEodAsync(stoppingToken);
        }
    }

    private async Task TryFetchEodAsync(CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;
        var todayUtc = DateOnly.FromDateTime(nowUtc);
        var currentTime = TimeOnly.FromDateTime(nowUtc);

        // Skip weekends
        if (nowUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return;

        foreach (var (zone, eodTime) in EodTimes)
        {
            // Only fetch if market has closed today AND we haven't fetched for today yet
            if (currentTime < eodTime) continue;
            if (_lastFetched.TryGetValue(zone, out var lastDate) && lastDate == todayUtc) continue;

            await FetchZoneEodAsync(zone, todayUtc, ct);
            _lastFetched[zone] = todayUtc;
        }
    }

    private async Task FetchZoneEodAsync(MarketZone zone, DateOnly date, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var finnhub = scope.ServiceProvider.GetRequiredService<IFinnhubService>();
        var priceRepo = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        var securities = await db.Securities
            .Where(s => s.MarketZone == zone)
            .ToListAsync(ct);

        if (securities.Count == 0) return;

        logger.LogInformation("Fetching EOD prices for {Zone} ({Count} securities) — {Date}",
            zone, securities.Count, date);

        foreach (var security in securities)
        {
            try
            {
                var quote = await finnhub.GetQuoteAsync(security.Symbol);
                if (quote is null) continue;

                var isEquity = security.AssetType == AssetType.Equity;
                var profile = isEquity ? await finnhub.GetProfileAsync(security.Symbol) : null;

                await priceRepo.UpsertAsync(security.Id, [new EodPrice
                {
                    Date = date,
                    Open = quote.Open,
                    High = quote.High,
                    Low = quote.Low,
                    Close = quote.PreviousClose, // After close, previousClose = today's settled EOD
                    Volume = 0,
                    MarketCapMillions = profile?.MarketCapMillions
                }]);

                logger.LogInformation("EOD stored: {Symbol} close={Close} mcap={MCap}M",
                    security.Symbol, quote.PreviousClose, profile?.MarketCapMillions);

                // Be kind to Finnhub rate limits
                await Task.Delay(500, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch EOD for {Symbol}", security.Symbol);
            }
        }

        logger.LogInformation("EOD fetch complete for {Zone}", zone);
    }
}
