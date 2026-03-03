using System.Text.Json;

namespace AthenaFinance.Api.Services;

/// <summary>
/// Forex rates via Frankfurter.app — free, no API key, ECB official daily rates.
/// https://www.frankfurter.app/docs
/// </summary>
public class FrankfurterService(HttpClient httpClient, ILogger<FrankfurterService> logger)
    : IForexService
{
    public async Task<(ForexRate? Today, ForexRate? Yesterday)> GetRatesAsync(string baseCcy, string quoteCcy)
    {
        var today = await FetchAsync("latest", baseCcy, quoteCcy);
        ForexRate? yesterday = null;

        if (today is not null)
        {
            // Fetch yesterday to calculate Day%
            var yesterdayDate = today.Date.AddDays(-1);
            // Skip back over weekends
            while (yesterdayDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                yesterdayDate = yesterdayDate.AddDays(-1);
            yesterday = await FetchAsync(yesterdayDate.ToString("yyyy-MM-dd"), baseCcy, quoteCcy);
        }

        return (today, yesterday);
    }

    public Task<ForexRate?> GetHistoricalRateAsync(string baseCcy, string quoteCcy, DateOnly date) =>
        FetchAsync(date.ToString("yyyy-MM-dd"), baseCcy, quoteCcy);

    private async Task<ForexRate?> FetchAsync(string dateOrLatest, string baseCcy, string quoteCcy)
    {
        try
        {
            var url = $"{dateOrLatest}?from={baseCcy}&to={quoteCcy}";
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("rates", out var rates)) return null;
            if (!rates.TryGetProperty(quoteCcy, out var rateEl)) return null;

            var date = DateOnly.Parse(root.GetProperty("date").GetString()!);
            return new ForexRate(baseCcy, quoteCcy, rateEl.GetDecimal(), date);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch forex rate {Base}/{Quote} for {Date}",
                baseCcy, quoteCcy, dateOrLatest);
            return null;
        }
    }
}
