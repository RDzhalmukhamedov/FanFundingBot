using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Orders.Queries;

public sealed record GetMyOrdersQuery(long UserId) : IRequest<Result<List<OrderDto>>>;

public class GetMyOrdersQueryHandler : IRequestHandler<GetMyOrdersQuery, Result<List<OrderDto>>>
{
    private readonly IOrderRepository _orders;

    public GetMyOrdersQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<Result<List<OrderDto>>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orders.GetByUserWithAddonsAsync(request.UserId);
        return Result.Ok(orders.Adapt<List<OrderDto>>());
    }
}

public sealed record GetOrderByIdQuery(Guid OrderId, long RequesterUserId) : IRequest<Result<OrderDto>>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;

    public GetOrderByIdQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdWithAddonsAsync(request.OrderId);
        if (order is null)
            return Result.Fail<OrderDto>("Заказ не найден.");

        if (order.User.Id != request.RequesterUserId)
            return Result.Fail<OrderDto>("Нет прав.");

        return Result.Ok(order.Adapt<OrderDto>());
    }
}
