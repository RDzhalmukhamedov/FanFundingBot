using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Бейдж статуса заказа (текст — из <see cref="OrderStatusTexts"/>).
/// </summary>
public partial class StatusBadge
{
    [Parameter, EditorRequired] public OrderStatus Status { get; set; }

    private string StatusText => OrderStatusTexts.Text(Status);
}
