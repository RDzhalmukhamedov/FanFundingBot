using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCanEditToProjectStatusEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CanEdit",
                table: "ProjectStatusEntries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Seed CanEdit: статусы 1-4 (Запланирован, Перевод, Вычитка, Подготовка макетов) — true, остальные — false
            migrationBuilder.Sql(@"
                UPDATE ""ProjectStatusEntries"" SET ""CanEdit"" = true WHERE ""Id"" IN (1, 2, 3, 4);
                UPDATE ""ProjectStatusEntries"" SET ""CanEdit"" = false WHERE ""Id"" IN (5, 6, 7, 8, 9);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CanEdit",
                table: "ProjectStatusEntries");
        }
    }
}
