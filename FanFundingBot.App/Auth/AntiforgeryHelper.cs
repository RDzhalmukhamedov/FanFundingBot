using Microsoft.AspNetCore.Antiforgery;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Явная проверка CSRF-токена в minimal API: встроенный middleware
/// по метаданным здесь не срабатывает (проверено curl: 302 без токена),
/// поэтому валидируем тем же штатным <see cref="IAntiforgery"/> вручную.
/// Токен — из заголовка <c>RequestVerificationToken</c> (fetch) или формы.
/// </summary>
public static class AntiforgeryHelper
{
    public static async Task<bool> HasValidTokenAsync(HttpContext http)
    {
        var antiforgery = http.RequestServices.GetRequiredService<IAntiforgery>();
        try
        {
            await antiforgery.ValidateRequestAsync(http);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
