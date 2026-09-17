using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using FanFundingBot.App.Auth;
using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components;

/// <summary>
/// База Blazor-страниц для прямых вызовов MediatR (без HTTP-loopback).
/// Перед каждым <c>Send</c> фиксирует UserId circuit'а в
/// <see cref="BlazorCircuitUserContext"/>, чтобы амбиентный
/// <c>ICurrentUserProvider</c> совпадал с полем <c>CallerUserId</c> запроса
/// (этого требует <c>AuthorizationBehavior</c>).
/// </summary>
[ExcludeFromCodeCoverage]
public abstract class AuthComponentBase : ComponentBase, IDisposable
{
    [Inject] protected AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] protected IMediator Mediator { get; set; } = null!;
    [Inject] protected BlazorCircuitUserContext CircuitUser { get; set; } = null!;
    [Inject] protected NavigationManager Nav { get; set; } = null!;

    private CancellationTokenSource? _cts;
    private bool _disposed;

    protected CancellationToken RequestCt => (_cts ??= new CancellationTokenSource()).Token;

    protected async Task<long> UseUserAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        var sub = state.User.FindFirst(JwtTokenService.SubjectClaim)?.Value;
        var id = long.TryParse(sub, out var parsed) && parsed > 0 ? parsed : 0;
        CircuitUser.UserId = id;
        return id;
    }

    protected static bool IsAuthenticated(ClaimsPrincipal user) =>
        user.Identity?.IsAuthenticated == true;

    protected void RedirectToLogin() => Nav.NavigateTo("/", forceLoad: true);

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        _disposed = true;
    }
}
