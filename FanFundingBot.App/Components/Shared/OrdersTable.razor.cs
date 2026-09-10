using FanFundingBot.Application.Orders.DTOs;
using Microsoft.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class OrdersTable
{
    [Parameter, EditorRequired] public List<OrderDto> Orders { get; set; } = [];
    [Parameter] public EventCallback<OrderDto> OnStatusChange { get; set; }
    [Parameter] public EventCallback<OrderDto> OnMessage { get; set; }

    private Task ChangeStatus(OrderDto order) => OnStatusChange.InvokeAsync(order);
    private Task WriteMessage(OrderDto order) => OnMessage.InvokeAsync(order);
}
