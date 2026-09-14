using System.Security.Claims;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Services;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Амбиентный пользователь: JWT (веб) с fallback на Telegram-контекст (бот).
/// Регистрируется ПОСЛЕ AddBotLayer, чтобы перекрыть TelegramCurrentUserProvider.
/// Без аутентифицированного HTTP-пользователя поведение 1:1 как у бота:
/// polling — нет HttpContext, webhook — не аутентифицирован.
/// </summary>
public sealed class HybridCurrentUserProvider : ICurrentUserProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TelegramUserContext _telegramContext;
    private readonly BlazorCircuitUserContext _circuitContext;

    public HybridCurrentUserProvider(
        IHttpContextAccessor httpContextAccessor,
        TelegramUserContext telegramContext,
        BlazorCircuitUserContext circuitContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _telegramContext = telegramContext;
        _circuitContext = circuitContext;
    }

    public long GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var sub = user.FindFirstValue(JwtTokenService.SubjectClaim);
            if (long.TryParse(sub, out var id) && id > 0)
                return id;
        }

        // Blazor Server interactive: HttpContext недоступен, берём id circuit'а
        // (выставляется из UserSession перед прямыми вызовами MediatR).
        if (_circuitContext.UserId > 0)
            return _circuitContext.UserId;

        return _telegramContext.UserId;
    }
}
