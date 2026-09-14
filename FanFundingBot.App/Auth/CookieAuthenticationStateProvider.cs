using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Состояние аутентификации Blazor Server из JWT-cookie.
/// Prerender: токен читается из cookie запроса, валидируется, в persisted state
/// кладётся только <see cref="UserSession"/> (сырой JWT клиенту не отдаём —
/// persisted state виден в HTML, в отличие от HttpOnly-cookie).
/// Интерактив (SignalR, без HttpContext): principal восстанавливается из сессии.
/// Совместимость: до Фазы 2 (прямые вызовы MediatR вместо HTTP-loopback)
/// legacy-ключ "auth_token" тоже персистится для <see cref="WebApiTokenAccessor"/>.
/// </summary>
public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string SessionKey = "auth_session";
    private const string LegacyTokenKey = "auth_token";

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly ClaimsPrincipal _user;

    public CookieAuthenticationStateProvider(
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor,
        IOptions<JwtOptions> options,
        WebApiTokenAccessor accessor,
        BlazorCircuitUserContext circuitContext)
    {
        string? token = null;
        UserSession? session = null;
        if (!state.TryTakeFromJson<UserSession?>(SessionKey, out var persisted) || persisted is null)
        {
            token = httpContextAccessor.HttpContext?.Request.Cookies[JwtTokenService.CookieName];
            session = ToSession(token, options.Value);
            state.RegisterOnPersisting(() =>
            {
                state.PersistAsJson(SessionKey, session);
                // TODO(Фаза 2): удалить вместе с WebApiTokenAccessor/AuthHeaderHandler.
                state.PersistAsJson(LegacyTokenKey, token);
                return Task.CompletedTask;
            });
        }
        else
        {
            session = persisted.Expires > DateTimeOffset.UtcNow && persisted.UserId > 0 ? persisted : null;
            // TODO(Фаза 2): удалить вместе с WebApiTokenAccessor/AuthHeaderHandler.
            state.TryTakeFromJson<string?>(LegacyTokenKey, out token);
        }

        accessor.Token = token;
        _user = session is null ? Anonymous : ToPrincipal(session);
        if (session is not null)
            circuitContext.UserId = session.UserId;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_user));

    private static TokenValidationParameters Validation(JwtOptions options) => new()
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret))
    };

    private static UserSession? ToSession(string? token, JwtOptions options)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(options.Secret))
            return null;

        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }
                .ValidateToken(token, Validation(options), out var validated);
            var sub = principal.FindFirst(JwtTokenService.SubjectClaim)?.Value;
            if (!long.TryParse(sub, out var id) || id <= 0)
                return null;

            return new UserSession
            {
                UserId = id,
                Role = principal.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty,
                Expires = new DateTimeOffset(validated.ValidTo, TimeSpan.Zero)
            };
        }
        catch
        {
            return null;
        }
    }

    private static ClaimsPrincipal ToPrincipal(UserSession session)
    {
        if (session.UserId <= 0)
            return Anonymous;

        var identity = new ClaimsIdentity(
        [
            new Claim(JwtTokenService.SubjectClaim, session.UserId.ToString()),
            new Claim(ClaimTypes.Role, session.Role)
        ], authenticationType: "cookie-session");

        return new ClaimsPrincipal(identity);
    }
}
