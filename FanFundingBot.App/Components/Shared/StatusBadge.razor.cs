using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Текст статуса заказа. Дублирует маппинг NotificationService/UIRenderer (там — для бота),
/// здесь — для веба. Держать синхронно.
/// </summary>
public partial class StatusBadge
{
    [Parameter, EditorRequired] public OrderStatus Status { get; set; }

    private string StatusText => Status switch
    {
        OrderStatus.New => "🆕 Новый",
        OrderStatus.Confirmed => "✅ Подтверждён",
        OrderStatus.Completed => "✔️ Выполнен",
        OrderStatus.Cancelled => "❌ Отменён",
        _ => Status.ToString()
    };
}
