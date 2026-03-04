using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace athena_finance_api.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceProvidersTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL cannot cast text→integer automatically when the column
            // already holds string codes ("finnhub", "yahoo").
            // Drop it first, recreate as integer FK after the PriceProviders table exists.
            migrationBuilder.DropColumn(
                name: "PriceSourceId",
                table: "Securities");

            migrationBuilder.CreateTable(
                name: "PriceProviders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    RequiresApiKey = table.Column<bool>(type: "boolean", nullable: false),
                    BaseUrl = table.Column<string>(type: "text", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceProviders", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "PriceProviders",
                columns: new[] { "Id", "BaseUrl", "Code", "Description", "IsActive", "Name", "Notes", "Priority", "RequiresApiKey" },
                values: new object[,]
                {
                    { 1, "https://finnhub.io/api/v1/", "finnhub", "Real-time & EOD data for US/CAD stocks. Free tier: 60 req/min.", true, "Finnhub", "Best coverage for US/NASDAQ/NYSE. Limited free-tier support for EU/ASIA.", 1, true },
                    { 2, "https://query1.finance.yahoo.com/", "yahoo", "Unofficial API. No key required. Global coverage (EU, ASIA, US).", true, "Yahoo Finance", "Unofficial — no SLA. Symbols use exchange suffixes: MC.PA, SIE.DE, 7203.T, 0700.HK etc.", 2, false },
                    { 3, "https://api.frankfurter.app/", "frankfurter", "Official ECB exchange rates. Free, no key, major currency pairs only.", true, "Frankfurter (ECB)", "Used exclusively for Forex. Only covers ECB-published pairs.", 3, false },
                    { 4, "https://api.polygon.io/", "polygon", "US stock universe bulk snapshots + OHLCV. Free tier: 15-min delayed.", true, "Polygon.io", "Used by UniverseSyncBackgroundService for bulk US EOD snapshots.", 4, true }
                });

            // Add PriceSourceId back as integer FK (null = no provider assigned)
            migrationBuilder.AddColumn<int>(
                name: "PriceSourceId",
                table: "Securities",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Securities_PriceSourceId",
                table: "Securities",
                column: "PriceSourceId");

            migrationBuilder.CreateIndex(
                name: "IX_PriceProviders_Code",
                table: "PriceProviders",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Securities_PriceProviders_PriceSourceId",
                table: "Securities",
                column: "PriceSourceId",
                principalTable: "PriceProviders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Securities_PriceProviders_PriceSourceId",
                table: "Securities");

            migrationBuilder.DropTable(
                name: "PriceProviders");

            migrationBuilder.DropIndex(
                name: "IX_Securities_PriceSourceId",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "PriceSourceId",
                table: "Securities");

            migrationBuilder.AddColumn<string>(
                name: "PriceSourceId",
                table: "Securities",
                type: "text",
                nullable: true);
        }
    }
}
