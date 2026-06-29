using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FanFundingBot.Data.Migrations
{
    public partial class ChangeStatusIdToInt : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop FK and index on Projects
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatusEntries_StatusId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_StatusId",
                table: "Projects");

            // 2. ProjectStatusEntries: add temp int column, copy Order values
            migrationBuilder.AddColumn<int>(
                name: "IdTemp",
                table: "ProjectStatusEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
                UPDATE ""ProjectStatusEntries"" SET ""IdTemp"" = ""Order""
            ");

            // 3. Projects: add temp int StatusId, map from old Guid FK
            migrationBuilder.AddColumn<int>(
                name: "StatusIdTemp",
                table: "Projects",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(@"
                UPDATE ""Projects"" p
                SET ""StatusIdTemp"" = t.""IdTemp""
                FROM ""ProjectStatusEntries"" t
                WHERE p.""StatusId"" = t.""Id""
            ");

            // 4. ProjectStatusEntries: drop old Guid Id, rename temp, drop Order PK constraint
            // In PostgreSQL the PK constraint is automatically created as PK_ProjectStatusEntries
            // by EF Core. We need to drop it before dropping the column.
            // But wait — we need to handle the PK correctly.
            // EF Core's generated PK has the same name in the model. Since we're dropping
            // and recreating, we use the explicit name.

            // Drop PK on old table, then drop old column
            // The PK was on Id (Guid). Drop PK constraint, then column.
            // Note: When you drop a column that is part of a PK, PostgreSQL requires
            // dropping the PK constraint first.
            // But in our case, the PK NAME is PK_ProjectStatusEntries.
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProjectStatusEntries"" DROP CONSTRAINT ""PK_ProjectStatusEntries""
            ");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ProjectStatusEntries");

            // Rename temp to Id
            migrationBuilder.RenameColumn(
                name: "IdTemp",
                table: "ProjectStatusEntries",
                newName: "Id");

            // 5. Projects: drop old Guid StatusId, rename temp
            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "StatusIdTemp",
                table: "Projects",
                newName: "StatusId");

            // 6. Set identity and PK on ProjectStatusEntries.Id
            // Set identity AFTER data is in place
            migrationBuilder.Sql(@"
                CREATE SEQUENCE IF NOT EXISTS ""ProjectStatusEntries_Id_seq""
                    START WITH 1
                    INCREMENT BY 1 NO MINVALUE NO MAXVALUE CACHE 1;
                ALTER TABLE ""ProjectStatusEntries"" ALTER COLUMN ""Id"" SET DEFAULT nextval('""ProjectStatusEntries_Id_seq""');
                ALTER SEQUENCE ""ProjectStatusEntries_Id_seq"" OWNED BY ""ProjectStatusEntries"".""Id"";
                SELECT setval('""ProjectStatusEntries_Id_seq""', (SELECT COALESCE(MAX(""Id"") + 1, 1) FROM ""ProjectStatusEntries""), false);
            ");

            // Add PK on new int Id
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProjectStatusEntries"" ADD CONSTRAINT ""PK_ProjectStatusEntries"" PRIMARY KEY (""Id"")
            ");

            // 7. Add FK and index back
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
            // 1. Drop FK and index
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_ProjectStatusEntries_StatusId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_StatusId",
                table: "Projects");

            // 2. PSE: add back Guid Id, fill from known mapping
            migrationBuilder.AddColumn<Guid>(
                name: "IdTmp",
                table: "ProjectStatusEntries",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql(@"
                UPDATE ""ProjectStatusEntries"" SET ""IdTmp"" = CASE ""Id""
                    WHEN 1 THEN '42ff25c3-59be-4dc8-9cb0-7d5032e57afb'
                    WHEN 2 THEN '5c1339a2-853e-4e80-9576-248dcad858dd'
                    WHEN 3 THEN 'fdabff28-2f9e-4e3e-9b41-1f043a641513'
                    WHEN 4 THEN '06af5b5f-bae8-45ff-950f-e196635df7b6'
                    WHEN 5 THEN '1643cc6a-990f-4b89-a201-7466762f88a5'
                    WHEN 6 THEN '737f5a6f-de0a-4235-9457-249f18fdf4aa'
                    WHEN 7 THEN '1c845489-df1f-411a-ad65-10e3bcac734f'
                    WHEN 8 THEN '8cf3ce75-49eb-4499-85ea-049409fe1f5e'
                    WHEN 9 THEN '380b6a3d-aa38-416f-9934-9981af998d0e'
                END
            ");

            // 3. Projects: add back Guid StatusId, map from known mapping
            migrationBuilder.AddColumn<Guid>(
                name: "StatusIdTmp",
                table: "Projects",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("42ff25c3-59be-4dc8-9cb0-7d5032e57afb"));

            migrationBuilder.Sql(@"
                UPDATE ""Projects"" p
                SET ""StatusIdTmp"" = t.""IdTmp""
                FROM ""ProjectStatusEntries"" t
                WHERE p.""StatusId"" = t.""Id""
            ");

            // 4. Drop PK and identity on int Id, drop column, rename Guid column
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProjectStatusEntries"" DROP CONSTRAINT ""PK_ProjectStatusEntries"";
                DROP SEQUENCE IF EXISTS ""ProjectStatusEntries_Id_seq"";
            ");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "ProjectStatusEntries");

            migrationBuilder.RenameColumn(
                name: "IdTmp",
                table: "ProjectStatusEntries",
                newName: "Id");

            // 5. Projects: drop int StatusId, rename Guid column
            migrationBuilder.DropColumn(
                name: "StatusId",
                table: "Projects");

            migrationBuilder.RenameColumn(
                name: "StatusIdTmp",
                table: "Projects",
                newName: "StatusId");

            // 6. Add PK back on Guid Id
            migrationBuilder.Sql(@"
                ALTER TABLE ""ProjectStatusEntries"" ADD CONSTRAINT ""PK_ProjectStatusEntries"" PRIMARY KEY (""Id"")
            ");

            // 7. FK and index
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
    }
}
