using AthenaFinance.Api.Data;
using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Services;

/// <summary>
/// Runs hourly. After each market zone closes, fetches and stores the EOD
/// price (close, high, low) and market cap for all securities in that zone.
/// Uses each security's stored PriceSourceId to pick the right provider.
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

    private readonly Dictionary<MarketZone, DateOnly> _lastFetched = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("EOD price background service started");

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        await TryFetchEodAsync(stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TryFetchEodAsync(stoppingToken);
        }
    }

    private async Task TryFetchEodAsync(CancellationToken ct)
    {
        var nowUtc     = DateTime.UtcNow;
        var todayUtc   = DateOnly.FromDateTime(nowUtc);
        var currentTime = TimeOnly.FromDateTime(nowUtc);

        if (nowUtc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            return;

        foreach (var (zone, eodTime) in EodTimes)
        {
            if (currentTime < eodTime) continue;
            if (_lastFetched.TryGetValue(zone, out var lastDate) && lastDate == todayUtc) continue;

            await FetchZoneEodAsync(zone, todayUtc, ct);
            _lastFetched[zone] = todayUtc;
        }
    }

    private async Task FetchZoneEodAsync(MarketZone zone, DateOnly date, CancellationToken ct)
    {
        using var scope     = scopeFactory.CreateScope();
        var db              = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var aggregator      = scope.ServiceProvider.GetRequiredService<MarketDataAggregator>();
        var forexSvc        = scope.ServiceProvider.GetRequiredService<IForexService>();
        var priceRepo       = scope.ServiceProvider.GetRequiredService<IPriceRepository>();

        var securities = await db.Securities
            .Include(s => s.PriceSource)
            .Where(s => s.MarketZone == zone)
            .ToListAsync(ct);

        if (securities.Count == 0) return;

        logger.LogInformation("Fetching EOD prices for {Zone} ({Count} securities) — {Date}",
            zone, securities.Count, date);

        // Fetch window: from yesterday to today (gives us last candle)
        var fromDt = date.ToDateTime(TimeOnly.MinValue).AddDays(-1);
        var toDt   = date.ToDateTime(TimeOnly.MaxValue);

        foreach (var security in securities)
        {
            try
            {
                decimal  close     = 0;
                decimal  high      = 0;
                decimal  low       = 0;
                decimal? marketCap = null;

                if (security.AssetType == AssetType.Forex)
                {
                    var parsed = IForexService.ParseSymbol(security.Symbol);
                    if (parsed is null) continue;
                    var rate = await forexSvc.GetHistoricalRateAsync(parsed.Value.Base, parsed.Value.Quote, date);
                    if (rate is null) continue;
                    close = high = low = rate.Rate;
                }
                else
                {
                    var sourceId     = security.EffectiveSourceCode;
                    var sourceSymbol = security.EffectiveSourceSymbol;

                    // Try EOD candles first (preferred — proper OHLCV)
                    var candles = await aggregator.GetEodPricesAsync(sourceId, sourceSymbol, fromDt, toDt);
                    if (candles.Count > 0)
                    {
                        var last = candles.Last();
                        close = last.Close;
                        high  = last.High;
                        low   = last.Low;
                    }
                    else
                    {
                        // Fallback: use live quote's previous close
                        var quote = await aggregator.GetQuoteAsync(sourceId, sourceSymbol);
                        if (quote is null) continue;
                        close = quote.PreviousClose ?? quote.CurrentPrice;
                        high  = close;
                        low   = close;
                    }

                    // Market cap (best-effort from profile)
                    if (security.AssetType == AssetType.Equity)
                    {
                        var (profile, _, _) = await aggregator.ResolveProfileAsync(
                            sourceSymbol, security.PriceSource?.Code, security.PriceSourceSymbol);
                        marketCap = profile?.MarketCapMillions;
                    }
                }

                await priceRepo.UpsertAsync(security.Id, [new EodPrice
                {
                    Date             = date,
                    Open             = close,
                    High             = high,
                    Low              = low,
                    Close            = close,
                    Volume           = 0,
                    MarketCapMillions = marketCap
                }]);

                logger.LogInformation("EOD stored: {Symbol} (via {Provider}) close={Close} mcap={MCap}M",
                    security.Symbol, security.EffectiveSourceCode, close, marketCap);

                await Task.Delay(300, ct); // gentle rate limit
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to fetch EOD for {Symbol}", security.Symbol);
            }
        }

        logger.LogInformation("EOD fetch complete for {Zone}", zone);
    }
}
