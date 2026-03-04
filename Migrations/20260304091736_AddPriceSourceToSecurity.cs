using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace athena_finance_api.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceSourceToSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PriceSourceId",
                table: "Securities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PriceSourceSymbol",
                table: "Securities",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PriceSourceId",
                table: "Securities");

            migrationBuilder.DropColumn(
                name: "PriceSourceSymbol",
                table: "Securities");
        }
    }
}
