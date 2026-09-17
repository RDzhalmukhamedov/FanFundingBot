using FanFundingBot.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class OrderStatusDialog : IDialogContentComponent<OrderStatusDialogData>
{
    [Parameter] public OrderStatusDialogData Content { get; set; } = new(OrderStatus.New);
    [CascadingParameter] public FluentDialog Dialog { get; set; } = null!;

    private static List<OrderStatus> OrderStatuses => Enum.GetValues<OrderStatus>().ToList();

    private OrderStatus _status = OrderStatus.New;

    protected override void OnParametersSet()
    {
        _status = Content.Current;
    }

    private async Task ConfirmAsync() => await Dialog.CloseAsync(DialogResult.Ok(_status));
    private async Task CancelAsync() => await Dialog.CancelAsync();
}

public sealed record OrderStatusDialogData(OrderStatus Current);
