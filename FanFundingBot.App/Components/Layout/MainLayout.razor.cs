using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Layout;

public partial class MainLayout
{
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool IsActive(string href)
    {
        var current = "/" + Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0].TrimEnd('/');
        return string.Equals(current, href, StringComparison.OrdinalIgnoreCase);
    }
}
