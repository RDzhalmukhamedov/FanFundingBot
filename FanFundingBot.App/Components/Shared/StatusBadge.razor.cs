using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Бейдж статуса заказа (текст — из <see cref="OrderStatusTexts"/>).
/// </summary>
public partial class StatusBadge
{
    [Parameter, EditorRequired] public OrderStatus Status { get; set; }

    private string StatusText => OrderStatusTexts.Text(Status);

    private Appearance BadgeAppearance => Status switch
    {
        OrderStatus.New => Appearance.Accent,
        OrderStatus.Confirmed => Appearance.Neutral,
        OrderStatus.Completed => Appearance.Lightweight,
        _ => Appearance.Outline
    };
}
