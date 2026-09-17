using System.Diagnostics.CodeAnalysis;

namespace FanFundingBot.App.Auth;

/// <summary>
/// UserId текущего Blazor-circuit. Заполняется из <see cref="UserSession"/>
/// перед прямыми вызовами MediatR (см. AuthComponentBase, Фаза 2), чтобы
/// амбиентный <c>ICurrentUserProvider</c> в circuit-scope возвращал того же
/// пользователя, что и поле <c>CallerUserId</c> запроса.
/// В скоупах бота/HTTP остаётся 0 и ни на что не влияет.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class BlazorCircuitUserContext
{
    public long UserId { get; set; }
}
