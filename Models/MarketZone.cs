namespace AthenaFinance.Api.Models;

/// <summary>
/// Market zone determines when EOD prices are captured.
/// All times are approximate UTC closes.
/// </summary>
public enum MarketZone
{
    US,   // NYSE/NASDAQ — ~21:00 UTC
    EU,   // LSE/Euronext/Xetra — ~17:30 UTC
    ASIA, // TSE/HKEX/ASX — ~08:00 UTC
    FX    // 24h forex — midnight UTC
}
