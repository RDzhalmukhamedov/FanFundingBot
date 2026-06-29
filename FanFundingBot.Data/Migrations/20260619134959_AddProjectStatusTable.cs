using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Data.Migrations
{
    public partial class AddProjectStatusTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectStatusEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectStatusEntries", x => x.Id);
                });

            // Seed known statuses
            migrationBuilder.InsertData(
                table: "ProjectStatusEntries",
                columns: new[] { "Id", "DisplayName", "Order" },
                values: new object[,]
                {
                    { new Guid("42ff25c3-59be-4dc8-9cb0-7d5032e57afb"), "Запланирован", 1 },
                    { new Guid("5c1339a2-853e-4e80-9576-248dcad858dd"), "Перевод", 2 },
                    { new Guid("fdabff28-2f9e-4e3e-9b41-1f043a641513"), "Вычитка", 3 },
                    { new Guid("06af5b5f-bae8-45ff-950f-e196635df7b6"), "Подготовка макетов", 4 },
                    { new Guid("1643cc6a-990f-4b89-a201-7466762f88a5"), "Печать", 5 },
                    { new Guid("737f5a6f-de0a-4235-9457-249f18fdf4aa"), "Сортировка", 6 },
                    { new Guid("1c845489-df1f-411a-ad65-10e3bcac734f"), "Отправка", 7 },
                    { new Guid("8cf3ce75-49eb-4499-85ea-049409fe1f5e"), "Завершён", 8 },
                    { new Guid("380b6a3d-aa38-416f-9934-9981af998d0e"), "Приостановлен", 9 }
                });

            // Add nullable StatusId column
            migrationBuilder.AddColumn<Guid>(
                name: "StatusId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            // Migrate existing status strings to StatusId
            migrationBuilder.Sql(@"
                UPDATE ""Projects""
                SET ""StatusId"" =
                    CASE ""Status""
                        WHEN 'Planned' THEN '42ff25c3-59be-4dc8-9cb0-7d5032e57afb'::uuid
                        WHEN 'InProgress' THEN '5c1339a2-853e-4e80-9576-248dcad858dd'::uuid
                        WHEN 'Completed' THEN '8cf3ce75-49eb-4499-85ea-049409fe1f5e'::uuid
                        WHEN 'OnHold' THEN '380b6a3d-aa38-416f-9934-9981af998d0e'::uuid
                    END
            ");

            // Set default for new records and make non-nullable
            migrationBuilder.AlterColumn<Guid>(
                name: "StatusId",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("42ff25c3-59be-4dc8-9cb0-7d5032e57afb"));

            // Drop old Status column
            migrationBuilder.DropColumn(
                name: "Status",
                table: "Projects");

            // Add FK and index
            migrationBuilder.CreateIndex(
                name: "IX_Projects_StatusId",
                table: "Projects",
                column: "StatusId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_ProjectStatusEntries_StatusId",
                table: "Projects",
                column: "StatusId",
                principalTable: "ProjectStatusEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Add Status column back
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Projects",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Planned");

            // Try to restore original status strings
            migrationBuilder.Sql(@"
                UPDATE ""Projects""
                SET ""Status"" =
                    CASE ""StatusId""
                        WHEN '42ff25c3-59be-4dc8-9cb0-7d5032e57afb'::uuid THEN 'Planned'
                        WHEN '5c1339a2-853e-4e80-9576-248dcad858dd'::uuid THEN 'InProgress'
                        WHEN 'fdabff28-2f9e-4e3e-9b41-1f043a641513'::uuid THEN 'InProgress'
                        WHEN '06af5b5f-bae8-45ff-950f-e196635df7b6'::uuid THEN 'InProgress'
                        WHEN '1643cc6a-990f-4b89-a201-7466762f88a5'::uuid THEN 'InProgress'
                        WHEN '737f5a6f-de0a-4235-9457-249f18fdf4aa'::uuid THEN 'InProgress'
                        WHEN '1c845489-df1f-411a-ad65-10e3bcac734f'::uuid THEN 'InProgress'
                        WHEN '8cf3ce75-49eb-4499-85ea-049409fe1f5e'::uuid THEN 'Completed'
                        WHEN '380b6a3d-aa38-416f-9934-9981af998d0e'::uuid THEN 'OnHold'
                        ELSE 'Planned'
                    END
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatusEntries_StatusId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_StatusId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "ProjectStatusEntries");
        }
    }
}
