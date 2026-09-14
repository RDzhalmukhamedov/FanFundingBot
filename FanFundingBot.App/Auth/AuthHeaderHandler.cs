using System.Net.Http.Headers;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Подставляет Bearer-токен текущего пользователя в вызовы к собственному Minimal API.
/// Cookie браузера серверный HttpClient не видит, поэтому токен берётся из <see cref="WebApiTokenAccessor"/>.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly WebApiTokenAccessor _accessor;

    public AuthHeaderHandler(WebApiTokenAccessor accessor)
    {
        _accessor = accessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(_accessor.Token) && request.Headers.Authorization is null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessor.Token);

        return base.SendAsync(request, cancellationToken);
    }
}
