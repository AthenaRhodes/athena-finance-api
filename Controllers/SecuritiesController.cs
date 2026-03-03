using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecuritiesController(ISecurityRepository repo, IFinnhubService finnhub) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await repo.GetAllAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var security = await repo.GetByIdAsync(id);
        return security is null ? NotFound() : Ok(security);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateSecurityRequest request)
    {
        var existing = await repo.GetBySymbolAsync(request.Symbol);
        if (existing is not null)
            return Conflict(new { message = $"Security '{request.Symbol}' already exists.", id = existing.Id });

        // Auto-resolve name and exchange from Finnhub for equities
        var name = request.Name;
        var exchange = request.Exchange ?? string.Empty;
        var currency = request.Currency ?? "USD";

        if (request.AssetType == AssetType.Equity && string.IsNullOrWhiteSpace(name))
        {
            var profile = await finnhub.GetProfileAsync(request.Symbol);
            if (profile is null)
                return BadRequest(new { message = $"Symbol '{request.Symbol}' not found on Finnhub." });
            name = profile.Name;
            exchange = string.IsNullOrWhiteSpace(exchange) ? profile.Exchange : exchange;
            currency = string.IsNullOrWhiteSpace(request.Currency) ? profile.Currency : currency;
        }

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Could not resolve name for this symbol." });

        // Default market zone by asset type if not specified
        var marketZone = request.MarketZone ?? request.AssetType switch
        {
            AssetType.Forex => MarketZone.FX,
            _ => MarketZone.US
        };

        var security = new Security
        {
            Symbol = request.Symbol,
            Name = name,
            AssetType = request.AssetType,
            Exchange = exchange,
            Currency = currency,
            MarketZone = marketZone
        };

        var created = await repo.CreateAsync(security);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await repo.DeleteAsync(id);
        return NoContent();
    }
}

public record CreateSecurityRequest(
    string Symbol,
    AssetType AssetType,
    string? Name,
    string? Exchange,
    string? Currency,
    MarketZone? MarketZone
);
