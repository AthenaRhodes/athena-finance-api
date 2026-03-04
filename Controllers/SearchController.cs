using AthenaFinance.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AthenaFinance.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SearchController(MarketDataAggregator aggregator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<object>());

        var results = await aggregator.SearchAsync(q);
        return Ok(results);
    }
}
