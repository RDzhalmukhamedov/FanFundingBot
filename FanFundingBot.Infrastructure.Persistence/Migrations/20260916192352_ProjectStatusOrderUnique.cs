using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProjectStatusOrderUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectStatusEntries_Order",
                table: "ProjectStatusEntries",
                column: "Order",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectStatusEntries_Order",
                table: "ProjectStatusEntries");
        }
    }
}
