using System.Text.Json;

namespace AthenaFinance.Api.Services;

public class FinnhubService(HttpClient httpClient, IConfiguration config, ILogger<FinnhubService> logger)
    : IFinnhubService
{
    private readonly string _apiKey = config["Finnhub:ApiKey"] ?? throw new InvalidOperationException("Finnhub:ApiKey not configured");

    public async Task<FinnhubQuote?> GetQuoteAsync(string symbol)
    {
        try
        {
            var response = await httpClient.GetAsync($"quote?symbol={symbol}&token={_apiKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            return new FinnhubQuote(
                CurrentPrice: root.GetProperty("c").GetDecimal(),
                Change: root.GetProperty("d").GetDecimal(),
                PercentChange: root.GetProperty("dp").GetDecimal(),
                High: root.GetProperty("h").GetDecimal(),
                Low: root.GetProperty("l").GetDecimal(),
                Open: root.GetProperty("o").GetDecimal(),
                PreviousClose: root.GetProperty("pc").GetDecimal()
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch quote for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<FinnhubMetrics?> GetMetricsAsync(string symbol)
    {
        try
        {
            var response = await httpClient.GetAsync($"stock/metric?symbol={symbol}&metric=all&token={_apiKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var m = doc.RootElement.GetProperty("metric");

            decimal? Get(string key) =>
                m.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.Number
                    ? el.GetDecimal() : null;
            string? GetStr(string key) =>
                m.TryGetProperty(key, out var el) && el.ValueKind == JsonValueKind.String
                    ? el.GetString() : null;

            return new FinnhubMetrics(
                YtdReturn:   Get("yearToDatePriceReturnDaily"),
                Return5D:    Get("5DayPriceReturnDaily"),
                Return13W:   Get("13WeekPriceReturnDaily"),
                Return26W:   Get("26WeekPriceReturnDaily"),
                Return52W:   Get("52WeekPriceReturnDaily"),
                High52W:     Get("52WeekHigh"),
                Low52W:      Get("52WeekLow"),
                High52WDate: GetStr("52WeekHighDate"),
                Low52WDate:  GetStr("52WeekLowDate")
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch metrics for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IList<FinnhubSearchResult>> SearchAsync(string query)
    {
        try
        {
            var response = await httpClient.GetAsync($"search?q={Uri.EscapeDataString(query)}&token={_apiKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            return doc.RootElement.GetProperty("result")
                .EnumerateArray()
                .Select(r => new FinnhubSearchResult(
                    Symbol: r.GetProperty("symbol").GetString() ?? string.Empty,
                    DisplaySymbol: r.GetProperty("displaySymbol").GetString() ?? string.Empty,
                    Description: r.GetProperty("description").GetString() ?? string.Empty,
                    Type: r.GetProperty("type").GetString() ?? string.Empty
                ))
                .Take(8)
                .ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to search for {Query}", query);
            return [];
        }
    }

    public async Task<FinnhubProfile?> GetProfileAsync(string symbol)
    {
        try
        {
            var response = await httpClient.GetAsync($"stock/profile2?symbol={symbol}&token={_apiKey}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            return new FinnhubProfile(
                Name: root.GetProperty("name").GetString() ?? symbol,
                Exchange: root.GetProperty("exchange").GetString() ?? string.Empty,
                Industry: root.GetProperty("finnhubIndustry").GetString() ?? string.Empty,
                Currency: root.GetProperty("currency").GetString() ?? "USD",
                Logo: root.GetProperty("logo").GetString() ?? string.Empty,
                WebUrl: root.GetProperty("weburl").GetString() ?? string.Empty,
                MarketCapMillions: root.GetProperty("marketCapitalization").GetDecimal()
            );
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch profile for {Symbol}", symbol);
            return null;
        }
    }

    public async Task<IList<FinnhubCandle>> GetEodPricesAsync(string symbol, DateTime from, DateTime to)
    {
        try
        {
            var fromTs = new DateTimeOffset(from).ToUnixTimeSeconds();
            var toTs = new DateTimeOffset(to).ToUnixTimeSeconds();
            var url = $"stock/candle?symbol={symbol}&resolution=D&from={fromTs}&to={toTs}&token={_apiKey}";
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            if (root.GetProperty("s").GetString() == "no_data")
                return [];

            var timestamps = root.GetProperty("t").EnumerateArray().ToList();
            var opens = root.GetProperty("o").EnumerateArray().ToList();
            var highs = root.GetProperty("h").EnumerateArray().ToList();
            var lows = root.GetProperty("l").EnumerateArray().ToList();
            var closes = root.GetProperty("c").EnumerateArray().ToList();
            var volumes = root.GetProperty("v").EnumerateArray().ToList();

            return timestamps.Select((t, i) => new FinnhubCandle(
                Date: DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(t.GetInt64()).UtcDateTime),
                Open: opens[i].GetDecimal(),
                High: highs[i].GetDecimal(),
                Low: lows[i].GetDecimal(),
                Close: closes[i].GetDecimal(),
                Volume: (long)volumes[i].GetDecimal()
            )).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch EOD prices for {Symbol}", symbol);
            return [];
        }
    }
}
