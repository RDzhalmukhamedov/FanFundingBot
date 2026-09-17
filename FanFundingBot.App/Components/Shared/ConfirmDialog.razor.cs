using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class ConfirmDialog : IDialogContentComponent<ConfirmDialogData>
{
    [Parameter] public ConfirmDialogData Content { get; set; } = new(string.Empty, string.Empty);
    [CascadingParameter] public FluentDialog Dialog { get; set; } = null!;

    private async Task ConfirmAsync() => await Dialog.CloseAsync(DialogResult.Ok(true));
    private async Task CancelAsync() => await Dialog.CancelAsync();
}

public sealed record ConfirmDialogData(string Title, string Text);
