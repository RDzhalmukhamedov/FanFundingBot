using System.Net.Http.Json;
using FanFundingBot.App.Auth;
using FanFundingBot.Application.Orders.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class MyOrders
{
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;
    [Inject] private WebApiTokenAccessor TokenAccessor { get; set; } = null!;
    [Inject] private ILogger<MyOrders> Logger { get; set; } = null!;

    private bool Loaded { get; set; }
    private List<OrderDto> Orders { get; set; } = [];
    private string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (state.User.Identity?.IsAuthenticated != true)
        {
            Nav.NavigateTo("/", forceLoad: true);
            return;
        }

        try
        {
            var client = WebApi.Client(HttpFactory, Nav, TokenAccessor);
            var resp = await client.GetAsync("api/my-orders");
            if (!resp.IsSuccessStatusCode)
            {
                Error = await WebApi.ReadErrorAsync(resp);
                Orders = [];
            }
            else
            {
                Orders = await resp.Content.ReadFromJsonAsync<List<OrderDto>>() ?? [];
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MyOrders load failed");
            Error = "Не удалось загрузить заказы.";
            Orders = [];
        }

        Loaded = true;
    }
}
