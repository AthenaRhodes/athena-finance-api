namespace AthenaFinance.Api.Models;

public class WatchlistItem
{
    public int Id { get; set; }
    public int SecurityId { get; set; }
    public Security Security { get; set; } = null!;
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}
