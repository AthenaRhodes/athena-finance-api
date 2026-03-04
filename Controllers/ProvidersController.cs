using AthenaFinance.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AthenaFinance.Api.Controllers;

/// <summary>Read-only view of registered price providers and their configuration.</summary>
[ApiController]
[Route("api/[controller]")]
public class ProvidersController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await db.PriceProviders.OrderBy(p => p.Priority).ToListAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var provider = await db.PriceProviders.FindAsync(id);
        return provider is null ? NotFound() : Ok(provider);
    }

    /// <summary>How many securities are using each provider.</summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = await db.PriceProviders
            .Select(p => new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Priority,
                p.IsActive,
                SecuritiesCount = p.Securities.Count
            })
            .OrderBy(p => p.Priority)
            .ToListAsync();
        return Ok(stats);
    }
}
