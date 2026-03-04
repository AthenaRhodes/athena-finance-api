using System.Text.Json;

namespace AthenaFinance.Api.Services;

/// <summary>
/// Yahoo Finance provider using the unofficial REST API (no key required).
/// Priority 2 — global coverage including all EU/ASIA exchanges.
/// LVMH = MC.PA, Siemens = SIE.DE, Nestle = NESN.SW, etc.
///
/// IMPORTANT: This is an unofficial API. All calls are wrapped in try/catch.
/// </summary>
public class YahooFinanceService(HttpClient httpClient, ILogger<YahooFinanceService> logger) : IMarketDataProvider
{
    public string ProviderId => "yahoo";
    public int Priority => 2;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ─── Search ─────────────────────────────────────────────────────────────

    public async Task<IList<ProviderSearchResult>> SearchAsync(string query)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v1/finance/search?q={Uri.EscapeDataString(query)}&quotesCount=10&newsCount=0&enableFuzzyQuery=false";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var quotes = doc.RootElement.GetProperty("quotes");

            var results = new List<ProviderSearchResult>();
            foreach (var q in quotes.EnumerateArray())
            {
                var quoteType = q.TryGetProperty("quoteType", out var qt) ? qt.GetString() ?? "" : "";
                if (quoteType != "EQUITY" && quoteType != "ETF" && quoteType != "CRYPTOCURRENCY") continue;

                var symbol = q.TryGetProperty("symbol", out var s) ? s.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(symbol)) continue;

                var shortname = q.TryGetProperty("shortname", out var sn) ? sn.GetString() ?? "" : "";
                var longname  = q.TryGetProperty("longname",  out var ln) ? ln.GetString() ?? "" : "";
                var exchDisp  = q.TryGetProperty("exchDisp",  out var ed) ? ed.GetString() ?? "" : "";
                var exchange  = q.TryGetProperty("exchange",  out var ex) ? ex.GetString() ?? "" : "";

                results.Add(new ProviderSearchResult(
                    ProviderId: "yahoo",
                    ProviderSymbol: symbol,
                    DisplaySymbol: symbol,
                    Description: !string.IsNullOrEmpty(longname) ? longname : shortname,
                    Type: quoteType == "EQUITY" ? "Common Stock" : quoteType,
                    Exchange: !string.IsNullOrEmpty(exchDisp) ? exchDisp : exchange));
            }
            return results;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Yahoo] Search failed for '{Query}'", query);
            return [];
        }
    }

    // ─── Profile ────────────────────────────────────────────────────────────

    public async Task<ProviderProfile?> GetProfileAsync(string providerSymbol)
    {
        try
        {
            // 1. Fetch quote for basic info
            var quoteUrl = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(providerSymbol)}";
            var quoteResp = await httpClient.GetAsync(quoteUrl);
            if (!quoteResp.IsSuccessStatusCode) return null;

            using var quoteDoc = await JsonDocument.ParseAsync(await quoteResp.Content.ReadAsStreamAsync());
            var resultArr = quoteDoc.RootElement
                .GetProperty("quoteResponse")
                .GetProperty("result");

            if (resultArr.GetArrayLength() == 0) return null;
            var q = resultArr[0];

            var name     = GetStr(q, "longName") ?? GetStr(q, "shortName") ?? providerSymbol;
            var exchange = GetStr(q, "fullExchangeName") ?? GetStr(q, "exchange") ?? "";
            var currency = GetStr(q, "currency") ?? "USD";
            var mcap     = q.TryGetProperty("marketCap", out var mc) && mc.ValueKind == JsonValueKind.Number
                ? mc.GetDecimal() / 1_000_000m : (decimal?)null;

            // 2. Fetch asset profile for industry (best-effort)
            string industry = "";
            try
            {
                var summaryUrl = $"https://query1.finance.yahoo.com/v10/finance/quoteSummary/{Uri.EscapeDataString(providerSymbol)}?modules=assetProfile";
                var summaryResp = await httpClient.GetAsync(summaryUrl);
                if (summaryResp.IsSuccessStatusCode)
                {
                    using var summaryDoc = await JsonDocument.ParseAsync(await summaryResp.Content.ReadAsStreamAsync());
                    var summaryResult = summaryDoc.RootElement
                        .GetProperty("quoteSummary")
                        .GetProperty("result");
                    if (summaryResult.GetArrayLength() > 0)
                    {
                        var assetProfile = summaryResult[0].GetProperty("assetProfile");
                        industry = GetStr(assetProfile, "industry") ?? "";
                    }
                }
            }
            catch { /* industry is optional */ }

            return new ProviderProfile(
                Name: name!,
                Exchange: exchange,
                Industry: industry,
                Currency: currency,
                Logo: null,   // Yahoo doesn't provide logos
                WebUrl: null,
                MarketCapMillions: mcap);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Yahoo] GetProfile failed for '{Symbol}'", providerSymbol);
            return null;
        }
    }

    // ─── Quote ──────────────────────────────────────────────────────────────

    public async Task<ProviderQuote?> GetQuoteAsync(string providerSymbol)
    {
        try
        {
            var url = $"https://query1.finance.yahoo.com/v7/finance/quote?symbols={Uri.EscapeDataString(providerSymbol)}";
            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var resultArr = doc.RootElement.GetProperty("quoteResponse").GetProperty("result");
            if (resultArr.GetArrayLength() == 0) return null;

            var q = resultArr[0];
            var price = GetDecimal(q, "regularMarketPrice");
            if (price is null) return null;

            return new ProviderQuote(
                CurrentPrice: price.Value,
                PercentChange: GetDecimal(q, "regularMarketChangePercent"),
                PreviousClose: GetDecimal(q, "regularMarketPreviousClose"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Yahoo] GetQuote failed for '{Symbol}'", providerSymbol);
            return null;
        }
    }

    // ─── YTD Return ─────────────────────────────────────────────────────────

    public async Task<decimal?> GetYtdReturnAsync(string providerSymbol)
    {
        try
        {
            var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1);
            var candles = await GetEodPricesAsync(providerSymbol, yearStart, DateTime.UtcNow);
            if (candles.Count < 2) return null;

            var firstClose = candles.First().Close;
            var lastClose  = candles.Last().Close;
            if (firstClose == 0) return null;

            return Math.Round((lastClose - firstClose) / firstClose * 100m, 4);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Yahoo] GetYtdReturn failed for '{Symbol}'", providerSymbol);
            return null;
        }
    }

    // ─── EOD Prices ─────────────────────────────────────────────────────────

    public async Task<IList<ProviderEodCandle>> GetEodPricesAsync(string providerSymbol, DateTime from, DateTime to)
    {
        try
        {
            var fromTs = new DateTimeOffset(from, TimeSpan.Zero).ToUnixTimeSeconds();
            var toTs   = new DateTimeOffset(to,   TimeSpan.Zero).ToUnixTimeSeconds();
            var url    = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(providerSymbol)}?interval=1d&period1={fromTs}&period2={toTs}";

            var response = await httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return [];

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            var resultArr = doc.RootElement.GetProperty("chart").GetProperty("result");
            if (resultArr.GetArrayLength() == 0) return [];

            var result     = resultArr[0];
            var timestamps = result.GetProperty("timestamp").EnumerateArray().ToList();
            var quoteBlock = result.GetProperty("indicators").GetProperty("quote")[0];

            var opens   = quoteBlock.GetProperty("open").EnumerateArray().ToList();
            var highs   = quoteBlock.GetProperty("high").EnumerateArray().ToList();
            var lows    = quoteBlock.GetProperty("low").EnumerateArray().ToList();
            var closes  = quoteBlock.GetProperty("close").EnumerateArray().ToList();
            var volumes = quoteBlock.GetProperty("volume").EnumerateArray().ToList();

            var candles = new List<ProviderEodCandle>();
            for (int i = 0; i < timestamps.Count; i++)
            {
                if (closes[i].ValueKind == JsonValueKind.Null) continue; // skip days with no data
                var date = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).UtcDateTime);
                candles.Add(new ProviderEodCandle(
                    Date:   date,
                    Open:   GetElemDecimal(opens,   i) ?? 0m,
                    High:   GetElemDecimal(highs,   i) ?? 0m,
                    Low:    GetElemDecimal(lows,    i) ?? 0m,
                    Close:  GetElemDecimal(closes,  i) ?? 0m,
                    Volume: GetElemLong(volumes, i) ?? 0L));
            }
            return candles;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Yahoo] GetEodPrices failed for '{Symbol}'", providerSymbol);
            return [];
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static string? GetStr(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal? GetDecimal(JsonElement el, string key) =>
        el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;

    private static decimal? GetElemDecimal(List<JsonElement> list, int i) =>
        i < list.Count && list[i].ValueKind == JsonValueKind.Number ? list[i].GetDecimal() : null;

    private static long? GetElemLong(List<JsonElement> list, int i) =>
        i < list.Count && list[i].ValueKind == JsonValueKind.Number ? list[i].GetInt64() : null;
}
