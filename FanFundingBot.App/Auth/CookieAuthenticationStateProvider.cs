using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FanFundingBot.Domain.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Состояние аутентификации Blazor Server из JWT-cookie.
/// Prerender: токен читается из cookie запроса, валидируется, в persisted state
/// кладётся только <see cref="UserSession"/> (сырой JWT клиенту не отдаём —
/// persisted state виден в HTML, в отличие от HttpOnly-cookie).
/// Интерактив (SignalR, без HttpContext): principal восстанавливается из сессии
/// и ревалидируется по БД каждые 30 минут (роль/свежесть).
/// </summary>
public sealed class CookieAuthenticationStateProvider : RevalidatingServerAuthenticationStateProvider
{
    private const string SessionKey = "auth_session";

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private static readonly TimeSpan RevalidationPeriod = TimeSpan.FromMinutes(30);

    private readonly BlazorCircuitUserContext _circuitContext;
    private readonly IUserRepository _users;

    private UserSession? _session;
    private ClaimsPrincipal _user = Anonymous;

    public CookieAuthenticationStateProvider(
        ILoggerFactory loggerFactory,
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor,
        IOptions<JwtOptions> options,
        BlazorCircuitUserContext circuitContext,
        IUserRepository users)
        : base(loggerFactory)
    {
        _circuitContext = circuitContext;
        _users = users;

        UserSession? session;
        if (!state.TryTakeFromJson<UserSession?>(SessionKey, out var persisted) || persisted is null)
        {
            var token = httpContextAccessor.HttpContext?.Request.Cookies[JwtTokenService.CookieName];
            session = ToSession(token, options.Value);
            state.RegisterOnPersisting(() =>
            {
                state.PersistAsJson(SessionKey, session);
                return Task.CompletedTask;
            });
        }
        else
        {
            session = persisted.Expires > DateTimeOffset.UtcNow && persisted.UserId > 0 ? persisted : null;
        }

        ApplySession(session);
    }

    protected override TimeSpan RevalidationInterval => RevalidationPeriod;

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState, CancellationToken cancellationToken)
    {
        UserSession? fresh = null;
        try
        {
            var user = _session is null
                ? null
                : await _users.GetByIdAsync(_session.UserId);

            var verdict = SessionRevalidation.Check(
                _session, user?.Id, user?.Role.ToString(), DateTimeOffset.UtcNow);
            if (verdict == SessionRevalidation.Verdict.Valid)
                return true;

            if (verdict == SessionRevalidation.Verdict.RefreshRole && user is not null && _session is not null)
            {
                fresh = new UserSession
                {
                    UserId = user.Id,
                    Role = user.Role.ToString(),
                    Expires = _session.Expires
                };
            }
        }
        catch
        {
            // Граница: недоступность БД не должна разлогинивать.
            return true;
        }

        ApplySession(fresh);
        return false;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_user));

    private void ApplySession(UserSession? session)
    {
        _session = session;
        _user = session is null ? Anonymous : ToPrincipal(session);
        _circuitContext.UserId = session?.UserId ?? 0;
    }

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
