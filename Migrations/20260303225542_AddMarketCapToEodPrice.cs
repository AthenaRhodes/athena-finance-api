using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace athena_finance_api.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketCapToEodPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarketCapMillions",
                table: "EodPrices",
                type: "numeric(24,6)",
                precision: 24,
                scale: 6,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketCapMillions",
                table: "EodPrices");
        }
    }
}
