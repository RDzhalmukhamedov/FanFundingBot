using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class MessageDialog : IDialogContentComponent<MessageDialogData>
{
    [Parameter] public MessageDialogData Content { get; set; } = new(string.Empty, string.Empty);
    [CascadingParameter] public FluentDialog Dialog { get; set; } = null!;

    private string _text = string.Empty;

    private async Task ConfirmAsync() => await Dialog.CloseAsync(DialogResult.Ok(_text));
    private async Task CancelAsync() => await Dialog.CancelAsync();
}

public sealed record MessageDialogData(string Title, string Placeholder);
