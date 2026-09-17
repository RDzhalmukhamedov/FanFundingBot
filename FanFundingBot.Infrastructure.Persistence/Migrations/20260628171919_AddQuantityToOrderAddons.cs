using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddQuantityToOrderAddons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "OrderAddons",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Quantity",
                table: "OrderAddons");
        }
    }
}
