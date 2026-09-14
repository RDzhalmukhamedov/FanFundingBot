using FanFundingBot.Application.Orders.DTOs;
using Microsoft.AspNetCore.Components;
using Microsoft.FluentUI.AspNetCore.Components;

namespace FanFundingBot.App.Components.Shared;

public partial class OrdersTable
{
    [Parameter, EditorRequired] public GridItemsProvider<OrderDto>? ItemsProvider { get; set; }
    [Parameter, EditorRequired] public PaginationState Pagination { get; set; } = new() { ItemsPerPage = 20 };
    [Parameter] public EventCallback<OrderDto> OnStatusChange { get; set; }
    [Parameter] public EventCallback<OrderDto> OnMessage { get; set; }

    private FluentDataGrid<OrderDto>? _grid;

    public Task RefreshAsync() => _grid?.RefreshDataAsync() ?? Task.CompletedTask;

    private Task ChangeStatus(OrderDto order) => OnStatusChange.InvokeAsync(order);
    private Task WriteMessage(OrderDto order) => OnMessage.InvokeAsync(order);
}
