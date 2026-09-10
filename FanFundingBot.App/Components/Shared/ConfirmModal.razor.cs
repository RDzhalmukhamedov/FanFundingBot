using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class ConfirmModal
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public string Title { get; set; } = "Подтверждение";
    [Parameter] public string Text { get; set; } = string.Empty;
    [Parameter] public string? Error { get; set; }
    [Parameter] public EventCallback OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private Task ConfirmAsync() => OnConfirm.InvokeAsync();
    private Task Cancel() => OnCancel.InvokeAsync();
}
