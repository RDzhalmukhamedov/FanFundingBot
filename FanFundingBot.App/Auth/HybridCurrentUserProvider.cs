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

    public HybridCurrentUserProvider(
        IHttpContextAccessor httpContextAccessor,
        TelegramUserContext telegramContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _telegramContext = telegramContext;
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

        return _telegramContext.UserId;
    }
}
