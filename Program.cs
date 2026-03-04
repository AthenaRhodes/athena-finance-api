using AthenaFinance.Api.Data;
using AthenaFinance.Api.Repositories;
using AthenaFinance.Api.Services;
using Microsoft.EntityFrameworkCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, services, config) => config
    .ReadFrom.Configuration(ctx.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/api-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 30));

builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// HTTP Client for Finnhub
builder.Services.AddHttpClient<IFinnhubService, FinnhubService>(client =>
{
    client.BaseAddress = new Uri("https://finnhub.io/api/v1/");
});

// HTTP Client for Yahoo Finance (unofficial, no key — global coverage)
builder.Services.AddHttpClient<YahooFinanceService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AthenaFinance/1.0)");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});

// Market data provider chain (Finnhub priority 1, Yahoo priority 2)
builder.Services.AddScoped<FinnhubMarketDataProvider>();
builder.Services.AddScoped<YahooFinanceService>();
builder.Services.AddScoped<IMarketDataProvider>(sp => sp.GetRequiredService<FinnhubMarketDataProvider>());
builder.Services.AddScoped<IMarketDataProvider>(sp => sp.GetRequiredService<YahooFinanceService>());
builder.Services.AddScoped<MarketDataAggregator>();

// HTTP Client for Frankfurter (forex rates — free, ECB official)
builder.Services.AddHttpClient<IForexService, FrankfurterService>(client =>
{
    client.BaseAddress = new Uri("https://api.frankfurter.app/");
});

// HTTP Client for Polygon.io (universe-scale EOD data)
builder.Services.AddHttpClient<IPolygonService, PolygonService>(client =>
{
    client.BaseAddress = new Uri("https://api.polygon.io/");
    client.Timeout = TimeSpan.FromMinutes(2); // Bulk snapshot can be large
});

// Repositories
builder.Services.AddScoped<ISecurityRepository, SecurityRepository>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();

// Background services
builder.Services.AddHostedService<EodPriceBackgroundService>();
builder.Services.AddHostedService<UniverseSyncBackgroundService>();

// CORS for local frontend dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("LocalDev", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors("LocalDev");
}

app.UseAuthorization();
app.MapControllers();

// Auto-migrate on startup in dev
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
