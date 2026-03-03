using System.Text.Json;

namespace AthenaFinance.Api.Services;

public class PolygonService(HttpClient httpClient, IConfiguration config, ILogger<PolygonService> logger)
    : IPolygonService
{
    private readonly string _apiKey = config["Polygon:ApiKey"]
        ?? throw new InvalidOperationException("Polygon:ApiKey not configured");

    public async Task<IList<PolygonSnapshot>> GetAllSnapshotsAsync()
    {
        var results = new List<PolygonSnapshot>();
        try
        {
            // Single call returns all US stock snapshots
            var response = await httpClient.GetAsync(
                $"v2/snapshot/locale/us/markets/stocks/tickers?include_otc=false&apiKey={_apiKey}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);

            if (!doc.RootElement.TryGetProperty("tickers", out var tickers)) return results;

            foreach (var t in tickers.EnumerateArray())
            {
                try
                {
                    var day = t.GetProperty("day");
                    results.Add(new PolygonSnapshot(
                        Ticker: t.GetProperty("ticker").GetString() ?? string.Empty,
                        Close: day.TryGetProperty("c", out var c) ? c.GetDecimal() : 0,
                        Open: day.TryGetProperty("o", out var o) ? o.GetDecimal() : 0,
                        High: day.TryGetProperty("h", out var h) ? h.GetDecimal() : 0,
                        Low: day.TryGetProperty("l", out var l) ? l.GetDecimal() : 0,
                        Volume: day.TryGetProperty("v", out var v) ? (long)v.GetDecimal() : 0,
                        DayChangePercent: t.TryGetProperty("todaysChangePerc", out var dp) ? dp.GetDecimal() : 0,
                        MarketCap: null // Not in snapshot — fetched separately via ticker details if needed
                    ));
                }
                catch { /* skip malformed entries */ }
            }

            logger.LogInformation("Polygon bulk snapshot: {Count} tickers loaded", results.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Polygon bulk snapshot");
        }
        return results;
    }

    public async Task<PolygonDailyOhlc?> GetDailyOhlcAsync(string ticker, DateOnly date)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"v1/open-close/{ticker}/{date:yyyy-MM-dd}?adjusted=true&apiKey={_apiKey}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            return new PolygonDailyOhlc(
                Ticker: ticker,
                Date: date,
                Open: root.GetProperty("open").GetDecimal(),
                High: root.GetProperty("high").GetDecimal(),
                Low: root.GetProperty("low").GetDecimal(),
                Close: root.GetProperty("close").GetDecimal(),
                Volume: (long)root.GetProperty("volume").GetDecimal()
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Polygon daily OHLC for {Ticker} {Date}", ticker, date);
            return null;
        }
    }

    public async Task<PolygonTickerDetail?> GetTickerDetailAsync(string ticker)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"v3/reference/tickers/{ticker}?apiKey={_apiKey}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var r = doc.RootElement.GetProperty("results");

            return new PolygonTickerDetail(
                Ticker: r.GetProperty("ticker").GetString() ?? ticker,
                Name: r.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                Market: r.TryGetProperty("market", out var m) ? m.GetString() ?? string.Empty : string.Empty,
                Locale: r.TryGetProperty("locale", out var loc) ? loc.GetString() ?? string.Empty : string.Empty,
                Type: r.TryGetProperty("type", out var tp) ? tp.GetString() ?? string.Empty : string.Empty,
                CurrencyName: r.TryGetProperty("currency_name", out var ccy) ? ccy.GetString() ?? "usd" : "usd",
                MarketCap: r.TryGetProperty("market_cap", out var mc) && mc.ValueKind == JsonValueKind.Number
                    ? mc.GetDecimal() / 1_000_000 : null, // Convert to millions
                Description: r.TryGetProperty("description", out var d) ? d.GetString() : null
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Polygon ticker detail for {Ticker}", ticker);
            return null;
        }
    }

    public async Task<IList<PolygonTickerDetail>> GetAllTickersAsync(string? type = null)
    {
        var results = new List<PolygonTickerDetail>();
        var cursor = string.Empty;

        try
        {
            do
            {
                var url = $"v3/reference/tickers?market=stocks&active=true&limit=1000&apiKey={_apiKey}";
                if (type is not null) url += $"&type={type}";
                if (!string.IsNullOrEmpty(cursor)) url += $"&cursor={cursor}";

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStreamAsync();
                using var doc = await JsonDocument.ParseAsync(json);
                var root = doc.RootElement;

                foreach (var r in root.GetProperty("results").EnumerateArray())
                {
                    results.Add(new PolygonTickerDetail(
                        Ticker: r.GetProperty("ticker").GetString() ?? string.Empty,
                        Name: r.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty,
                        Market: r.TryGetProperty("market", out var m) ? m.GetString() ?? string.Empty : string.Empty,
                        Locale: r.TryGetProperty("locale", out var loc) ? loc.GetString() ?? string.Empty : string.Empty,
                        Type: r.TryGetProperty("type", out var tp) ? tp.GetString() ?? string.Empty : string.Empty,
                        CurrencyName: r.TryGetProperty("currency_name", out var ccy) ? ccy.GetString() ?? "usd" : "usd",
                        MarketCap: null,
                        Description: null
                    ));
                }

                cursor = root.TryGetProperty("next_cursor", out var nc) ? nc.GetString() ?? string.Empty : string.Empty;

            } while (!string.IsNullOrEmpty(cursor));

            logger.LogInformation("Polygon tickers loaded: {Count}", results.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Polygon ticker list");
        }

        return results;
    }
}
