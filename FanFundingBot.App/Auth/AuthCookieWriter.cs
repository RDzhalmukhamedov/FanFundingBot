namespace FanFundingBot.App.Auth;

/// <summary>
/// Единое место записи/удаления auth-cookie. Используется login/logout/refresh
/// эндпоинтами и sliding re-issue в <c>JwtBearerEvents.OnTokenValidated</c>.
/// </summary>
public static class AuthCookieWriter
{
    public static void Append(HttpResponse response, string token, int expiresDays, bool isHttps)
    {
        response.Cookies.Append(JwtTokenService.CookieName, token, new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = isHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(expiresDays)
        });
    }

    public static void Clear(HttpResponse response, bool isHttps)
    {
        // Delete должен совпасть по Secure-флагу с Append: иначе Secure-cookie не сотрётся
        // при downgrade http→https / прокси без ForwardedHeaders. Удаляем оба варианта.
        foreach (var secure in new[] { isHttps, true, false })
        {
            response.Cookies.Delete(JwtTokenService.CookieName, new CookieOptions
            {
                Path = "/",
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = secure
            });
        }
    }
}
