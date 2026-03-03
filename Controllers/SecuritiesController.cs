using AthenaFinance.Api.Models;
using AthenaFinance.Api.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecuritiesController(ISecurityRepository repo) : ControllerBase
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
            return Conflict(new { message = $"Security '{request.Symbol}' already exists." });

        var security = new Security
        {
            Symbol = request.Symbol,
            Name = request.Name,
            AssetType = request.AssetType,
            Exchange = request.Exchange ?? string.Empty,
            Currency = request.Currency ?? "USD"
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
    string Name,
    AssetType AssetType,
    string? Exchange,
    string? Currency
);
