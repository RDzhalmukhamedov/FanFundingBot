using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase0_RichModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Users_AnsweredByUserId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Users_UserId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderAddons_Addons_AddonId",
                table: "OrderAddons");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderAddons_Orders_OrderId",
                table: "OrderAddons");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderAddons",
                table: "OrderAddons");

            migrationBuilder.RenameTable(
                name: "OrderAddons",
                newName: "OrderPositions");

            migrationBuilder.RenameIndex(
                name: "IX_OrderAddons_AddonId",
                table: "OrderPositions",
                newName: "IX_OrderPositions_AddonId");

            migrationBuilder.RenameColumn(
                name: "AnsweredByUserId",
                table: "Questions",
                newName: "AnsweredById");

            migrationBuilder.RenameIndex(
                name: "IX_Questions_AnsweredByUserId",
                table: "Questions",
                newName: "IX_Questions_AnsweredById");

            migrationBuilder.RenameColumn(
                name: "CanEdit",
                table: "ProjectStatusEntries",
                newName: "CanEditOrders");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Orders",
                newName: "CustomerId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_UserId",
                table: "Orders",
                newName: "IX_Orders_CustomerId");

            migrationBuilder.AddColumn<string>(
                name: "Discriminator",
                table: "Users",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "OrderType",
                table: "Orders",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "Addons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderPositions",
                table: "OrderPositions",
                columns: new[] { "OrderId", "AddonId" });

            migrationBuilder.AddForeignKey(
                name: "FK_OrderPositions_Addons_AddonId",
                table: "OrderPositions",
                column: "AddonId",
                principalTable: "Addons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderPositions_Orders_OrderId",
                table: "OrderPositions",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_CustomerId",
                table: "Orders",
                column: "CustomerId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Users_AnsweredById",
                table: "Questions",
                column: "AnsweredById",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Users_UserId",
                table: "Questions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // Backfill новых non-null колонок для существующих строк.
            // Все исторические заказы были сформированными, проекты и дополнения — вне режима правок.
            migrationBuilder.Sql("UPDATE \"Orders\" SET \"OrderType\" = 'Formed' WHERE \"OrderType\" = '';");
            migrationBuilder.Sql("UPDATE \"Users\" SET \"Discriminator\" = CASE WHEN \"Role\" = 'Admin' THEN 'Admin' ELSE 'Customer' END WHERE \"Discriminator\" = '';");
            migrationBuilder.Sql("UPDATE \"Projects\" SET \"Mode\" = 1 WHERE \"Mode\" = 0;");
            migrationBuilder.Sql("UPDATE \"Addons\" SET \"Mode\" = 1 WHERE \"Mode\" = 0;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Orders_Users_CustomerId",
                table: "Orders");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Users_AnsweredById",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Users_UserId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderPositions_Addons_AddonId",
                table: "OrderPositions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderPositions_Orders_OrderId",
                table: "OrderPositions");

            migrationBuilder.DropPrimaryKey(
                name: "PK_OrderPositions",
                table: "OrderPositions");

            migrationBuilder.RenameTable(
                name: "OrderPositions",
                newName: "OrderAddons");

            migrationBuilder.RenameIndex(
                name: "IX_OrderPositions_AddonId",
                table: "OrderAddons",
                newName: "IX_OrderAddons_AddonId");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OrderAddons",
                table: "OrderAddons",
                columns: new[] { "OrderId", "AddonId" });

            migrationBuilder.DropColumn(
                name: "Discriminator",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OrderType",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "Addons");

            migrationBuilder.RenameColumn(
                name: "AnsweredById",
                table: "Questions",
                newName: "AnsweredByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_Questions_AnsweredById",
                table: "Questions",
                newName: "IX_Questions_AnsweredByUserId");

            migrationBuilder.RenameColumn(
                name: "CanEditOrders",
                table: "ProjectStatusEntries",
                newName: "CanEdit");

            migrationBuilder.RenameColumn(
                name: "CustomerId",
                table: "Orders",
                newName: "UserId");

            migrationBuilder.RenameIndex(
                name: "IX_Orders_CustomerId",
                table: "Orders",
                newName: "IX_Orders_UserId");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Projects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderAddons_Addons_AddonId",
                table: "OrderAddons",
                column: "AddonId",
                principalTable: "Addons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderAddons_Orders_OrderId",
                table: "OrderAddons",
                column: "OrderId",
                principalTable: "Orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Orders_Users_UserId",
                table: "Orders",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Users_AnsweredByUserId",
                table: "Questions",
                column: "AnsweredByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Users_UserId",
                table: "Questions",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
