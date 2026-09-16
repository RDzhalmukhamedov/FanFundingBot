using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FanFundingBot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UserPhoneBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // User.Phone теперь маппится через PhoneNumber.Create: невалидное значение
            // бросало бы при материализации. Нормализуем форматирование там, где после
            // strip получается валидный номер, остальное невалидное -> NULL
            // (перевведется при следующем заказе через CreateOrderCommand).
            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Phone" = regexp_replace("Phone", '[\s\-\(\)]', '', 'g')
                WHERE "Phone" IS NOT NULL
                  AND regexp_replace("Phone", '[\s\-\(\)]', '', 'g') ~ '^(\+7|8)[0-9]{10}$';
                """);
            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Phone" = NULL
                WHERE "Phone" IS NOT NULL
                  AND NOT ("Phone" ~ '^(\+7|8)[0-9]{10}$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: backfill данных необратим (исходные невалидные значения не восстанавливаются).
        }
    }
}
