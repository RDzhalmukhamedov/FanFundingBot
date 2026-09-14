using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

/// <summary>
/// Редирект анонима на вход с returnUrl (полная перезагрузка — кладём цель в query).
/// </summary>
public partial class LoginRequired
{
    [Inject] private NavigationManager Nav { get; set; } = null!;

    protected override void OnInitialized()
    {
        var relative = "/" + Nav.ToBaseRelativePath(Nav.Uri);
        Nav.NavigateTo("/?returnUrl=" + Uri.EscapeDataString(relative), forceLoad: true);
    }
}
