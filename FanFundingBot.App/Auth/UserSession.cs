using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Минимум данных сессии для Blazor-circuit. Сырой JWT сюда не кладём:
/// persisted state виден клиенту, а HttpOnly-cookie — нет.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class UserSession
{
    public long UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset Expires { get; set; }
}
