using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecuritiesController(ISecurityRepository repo, MarketDataAggregator aggregator) : ControllerBase
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

        var name     = request.Name;
        var exchange = request.Exchange ?? string.Empty;
        var currency = request.Currency ?? "USD";
        string? resolvedProviderId     = request.PriceSourceId;
        string? resolvedProviderSymbol = request.PriceSourceSymbol;

        if (request.AssetType == AssetType.Equity && string.IsNullOrWhiteSpace(name))
        {
            // Try the preferred provider first (passed by the frontend from search results),
            // then fall back through the full provider chain.
            var (profile, foundProviderId, foundSymbol) = await aggregator.ResolveProfileAsync(
                request.Symbol,
                request.PriceSourceId,
                request.PriceSourceSymbol);

            if (profile is null)
                return BadRequest(new { message = $"Symbol '{request.Symbol}' not found on any data provider." });

            name     = profile.Name;
            exchange = string.IsNullOrWhiteSpace(exchange) ? profile.Exchange : exchange;
            currency = string.IsNullOrWhiteSpace(request.Currency) ? profile.Currency : currency;
            resolvedProviderId     = foundProviderId;
            resolvedProviderSymbol = foundSymbol;
        }

        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { message = "Could not resolve name for this symbol." });

        var marketZone = request.MarketZone ?? request.AssetType switch
        {
            AssetType.Forex => MarketZone.FX,
            _ => MarketZone.US
        };

        // Only store PriceSourceSymbol if it differs from Symbol (saves space, avoids confusion)
        var symbolUpper = request.Symbol.ToUpperInvariant();
        var effectiveProviderSymbol = resolvedProviderSymbol != symbolUpper ? resolvedProviderSymbol : null;

        // For Forex, use Frankfurter (ECB) — no market data provider needed for lookup
        if (request.AssetType == AssetType.Forex)
        {
            resolvedProviderId     = "frankfurter";
            effectiveProviderSymbol = null;
        }

        var security = new Security
        {
            Symbol         = symbolUpper,
            Name           = name,
            AssetType      = request.AssetType,
            Exchange       = exchange,
            Currency       = currency,
            MarketZone     = marketZone,
            PriceSourceId  = resolvedProviderId,
            PriceSourceSymbol = effectiveProviderSymbol
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
    MarketZone? MarketZone,
    string? PriceSourceId,       // from search result: "finnhub" or "yahoo"
    string? PriceSourceSymbol    // provider-specific symbol, e.g. "MC.PA"
);
