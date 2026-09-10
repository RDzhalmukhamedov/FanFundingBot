using System.Net.Http.Json;
using FanFundingBot.Application.Orders.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace FanFundingBot.App.Components.Pages;

public partial class MyOrders
{
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = null!;
    [Inject] private IHttpClientFactory HttpFactory { get; set; } = null!;
    [Inject] private NavigationManager Nav { get; set; } = null!;

    private bool Loaded { get; set; }
    private List<OrderDto> Orders { get; set; } = [];

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
            var client = WebApi.Client(HttpFactory, Nav);
            Orders = await client.GetFromJsonAsync<List<OrderDto>>("api/my-orders") ?? [];
        }
        catch
        {
            Orders = [];
        }

        Loaded = true;
    }
}
