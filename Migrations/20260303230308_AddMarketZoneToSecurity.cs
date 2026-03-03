using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace athena_finance_api.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketZoneToSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MarketZone",
                table: "Securities",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarketZone",
                table: "Securities");
        }
    }
}
