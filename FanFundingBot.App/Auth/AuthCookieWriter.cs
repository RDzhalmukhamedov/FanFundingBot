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
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Secure = isHttps,
            Expires = DateTimeOffset.UtcNow.AddDays(expiresDays)
        });
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(JwtTokenService.CookieName);
    }
}
