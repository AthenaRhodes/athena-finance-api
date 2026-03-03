using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PricesController(
    ISecurityRepository securityRepo,
    IPriceRepository priceRepo,
    IFinnhubService finnhub) : ControllerBase
{
    [HttpGet("{securityId:int}")]
    public async Task<IActionResult> GetPrices(
        int securityId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        var dateFrom = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var dateTo = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var prices = await priceRepo.GetPricesAsync(securityId, dateFrom, dateTo);
        return Ok(prices);
    }

    [HttpPost("{securityId:int}/fetch")]
    public async Task<IActionResult> FetchAndStore(
        int securityId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var security = await securityRepo.GetByIdAsync(securityId);
        if (security is null) return NotFound();

        var dateFrom = from ?? DateTime.UtcNow.AddDays(-30);
        var dateTo = to ?? DateTime.UtcNow;

        var candles = await finnhub.GetEodPricesAsync(security.Symbol, dateFrom, dateTo);
        var prices = candles.Select(c => new EodPrice
        {
            Date = c.Date,
            Open = c.Open,
            High = c.High,
            Low = c.Low,
            Close = c.Close,
            Volume = c.Volume
        });

        await priceRepo.UpsertAsync(securityId, prices);
        return Ok(new { fetched = candles.Count, symbol = security.Symbol });
    }
}
