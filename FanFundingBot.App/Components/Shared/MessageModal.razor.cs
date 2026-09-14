using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class MessageModal
{
    [Parameter] public bool Visible { get; set; }
    [Parameter] public string Title { get; set; } = "Сообщение";
    [Parameter] public string Text { get; set; } = string.Empty;
    [Parameter] public string? Error { get; set; }
    [Parameter] public EventCallback<string> OnConfirm { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }

    private string _text = string.Empty;
    private bool _wasVisible;

    protected override void OnParametersSet()
    {
        // Сброс при каждом открытии: иначе виден текст прошлого сообщения.
        if (Visible && !_wasVisible)
            _text = Text;

        _wasVisible = Visible;
    }

    private Task ConfirmAsync() => OnConfirm.InvokeAsync(_text);
    private Task Cancel() => OnCancel.InvokeAsync();
}
