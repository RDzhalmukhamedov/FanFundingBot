using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Проверка Telegram OIDC id_token (новая Login library, telegram-login.js):
/// подпись по JWKS Telegram, iss, aud == ClientId, срок действия.
/// Чистая функция — детерминирована через <paramref name="now"/> для тестов.
/// Невалидный токен — это обычный неуспех (null), не исключение.
/// </summary>
public static class TelegramIdTokenValidator
{
    public const string Issuer = "https://oauth.telegram.org";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    public static TelegramOidcUser? Validate(
        string? idToken,
        string clientId,
        JsonWebKeySet jwks,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(idToken)
            || string.IsNullOrWhiteSpace(clientId)
            || jwks.Keys.Count == 0)
            return null;

        var nowUtc = (now ?? DateTimeOffset.UtcNow).UtcDateTime;

        ClaimsPrincipal principal;
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            principal = handler.ValidateToken(idToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = Issuer,
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                ClockSkew = ClockSkew,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = jwks.Keys,
                // Детерминированное время для тестов; в проде равно UtcNow.
                LifetimeValidator = (notBefore, expires, _, _) =>
                    expires is not null && expires.Value.Add(ClockSkew) >= nowUtc
                    && (notBefore is null || notBefore.Value.Subtract(ClockSkew) <= nowUtc),
                RequireExpirationTime = true,
            }, out var validatedToken);

            // Пояс: принимаем только подписанные JWT (не alg=none).
            if (validatedToken is not JwtSecurityToken jwt
                || string.IsNullOrEmpty(jwt.RawSignature))
                return null;
        }
        catch
        {
            return null;
        }

        var idRaw = Claim(principal, "id") ?? Claim(principal, "sub");
        if (!long.TryParse(idRaw, out var id) || id <= 0)
            return null;

        var firstName = Claim(principal, "given_name");
        var lastName = Claim(principal, "family_name");
        if (firstName is null && Claim(principal, "name") is { } fullName)
            (firstName, lastName) = SplitName(fullName);

        return new TelegramOidcUser(
            id,
            firstName,
            lastName,
            Claim(principal, "preferred_username") ?? Claim(principal, "username"),
            Claim(principal, "picture") ?? Claim(principal, "photo_url"));
    }

    private static string? Claim(ClaimsPrincipal principal, string type)
    {
        var value = principal.FindFirst(type)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static (string? First, string? Last) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], parts[1]),
        };
    }
}
