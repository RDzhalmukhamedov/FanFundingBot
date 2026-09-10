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
/// Prerender: токен читается из cookie запроса. Интерактив (SignalR, без HttpContext):
/// токен берётся из persisted state. Распарсенный токен кладётся в <see cref="WebApiTokenAccessor"/>
/// для исходящих вызовов к собственному API.
/// </summary>
public sealed class CookieAuthenticationStateProvider : AuthenticationStateProvider
{
    private const string PersistKey = "auth_token";

    private static readonly ClaimsPrincipal Anonymous = new(new ClaimsIdentity());

    private readonly ClaimsPrincipal _user;

    public CookieAuthenticationStateProvider(
        PersistentComponentState state,
        IHttpContextAccessor httpContextAccessor,
        IOptions<JwtOptions> options,
        WebApiTokenAccessor accessor)
    {
        string? token = null;
        if (!state.TryTakeFromJson<string?>(PersistKey, out var persisted) || string.IsNullOrEmpty(persisted))
        {
            token = httpContextAccessor.HttpContext?.Request.Cookies[JwtTokenService.CookieName];
            state.RegisterOnPersisting(() =>
            {
                state.PersistAsJson(PersistKey, token);
                return Task.CompletedTask;
            });
        }
        else
        {
            token = persisted;
        }

        accessor.Token = token;
        _user = Validate(token, options.Value);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
        => Task.FromResult(new AuthenticationState(_user));

    private static ClaimsPrincipal Validate(string? token, JwtOptions options)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(options.Secret))
            return Anonymous;

        try
        {
            var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret))
                },
                out _);
            return principal;
        }
        catch
        {
            return Anonymous;
        }
    }
}
