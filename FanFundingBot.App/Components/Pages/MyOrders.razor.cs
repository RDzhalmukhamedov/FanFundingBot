using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Orders.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Pages;

public partial class MyOrders : AuthComponentBase
{
    [Inject] private ILogger<MyOrders> Logger { get; set; } = null!;

    private bool Loaded { get; set; }
    private List<OrderDto> Orders { get; set; } = [];
    private string? Error { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var state = await AuthState.GetAuthenticationStateAsync();
        if (!IsAuthenticated(state.User))
        {
            RedirectToLogin();
            return;
        }

        try
        {
            var uid = await UseUserAsync();
            var result = await Mediator.Send(new GetMyOrdersQuery(uid), RequestCt);
            if (result.IsSuccess)
            {
                Orders = result.Value;
            }
            else
            {
                Error = BlazorResult.Message(result);
                Orders = [];
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
