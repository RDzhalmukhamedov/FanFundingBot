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
            // бросало бы при материализации. Нормализуем форматирование (strip + legacy
            // 8XXXXXXXXXX -> +7XXXXXXXXXX), остальное невалидное -> NULL
            // (перевведется при следующем заказе через CreateOrderCommand).
            // Контракт 1:1 с Domain.Extensions.StringExtensions: ^\+\d{8,15}$.
            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Phone" =
                  CASE
                    WHEN regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') ~ '^8[0-9]{10}$'
                      THEN '+7' || substring(regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') from 2)
                    WHEN regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') ~ '^7[0-9]{10}$'
                      THEN '+' || regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g')
                    ELSE regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g')
                  END
                WHERE "Phone" IS NOT NULL;
                """);
            migrationBuilder.Sql(
                """
                UPDATE "Users" SET "Phone" = NULL
                WHERE "Phone" IS NOT NULL
                  AND NOT ("Phone" ~ '^\+\d{8,15}$');
                """);

            // Orders.Phone — IsRequired + PhoneNumber.Empty ("" для Draft без телефона).
            // Отличаем Draft без телефона ("") от битого Formed: нормализуем strip+8→+7 везде,
            // затем невалидные непустые -> "" (битый Formed станет "", но отличим по OrderType).
            // \s оставляем как есть (покрывает \n\r\t), 1:1 с Domain не требуется для SQL.
            migrationBuilder.Sql(
                """
                UPDATE "Orders" SET "Phone" =
                  CASE
                    WHEN regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') ~ '^8[0-9]{10}$'
                      THEN '+7' || substring(regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') from 2)
                    WHEN regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g') ~ '^7[0-9]{10}$'
                      THEN '+' || regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g')
                    ELSE regexp_replace("Phone", '[\s\-\(\)\.]', '', 'g')
                  END
                WHERE "Phone" IS NOT NULL
                  AND "Phone" <> '';
                """);
            migrationBuilder.Sql(
                """
                UPDATE "Orders" SET "Phone" = ''
                WHERE "Phone" IS NOT NULL
                  AND "Phone" <> ''
                  AND NOT ("Phone" ~ '^\+\d{8,15}$');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: backfill данных необратим (исходные невалидные значения не восстанавливаются).
        }
    }
}
