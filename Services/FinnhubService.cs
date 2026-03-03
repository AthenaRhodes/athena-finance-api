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
