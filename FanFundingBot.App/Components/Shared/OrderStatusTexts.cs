using FanFundingBot.Domain.Enums;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Единый текст статуса заказа для веба (StatusBadge + option'ы селектов).
/// Маппинг бота (NotificationService/UIRenderer) держать синхронно.
/// </summary>
public static class OrderStatusTexts
{
    public static string Text(OrderStatus status) => status switch
    {
        OrderStatus.New => "🆕 Новый",
        OrderStatus.Confirmed => "✅ Подтверждён",
        OrderStatus.Completed => "✔️ Выполнен",
        OrderStatus.Cancelled => "❌ Отменён",
        _ => status.ToString()
    };
}
