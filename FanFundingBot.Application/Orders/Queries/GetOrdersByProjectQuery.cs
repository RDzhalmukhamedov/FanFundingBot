using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Orders.Queries;

public sealed record GetOrdersByProjectQuery(Guid ProjectId, long CallerUserId)
    : IRequest<Result<List<OrderDto>>>;

public class GetOrdersByProjectQueryHandler : IRequestHandler<GetOrdersByProjectQuery, Result<List<OrderDto>>>
{
    private readonly IOrderRepository _orders;
    private readonly IAuthorizationService _authorization;

    public GetOrdersByProjectQueryHandler(IOrderRepository orders, IAuthorizationService authorization)
    {
        _orders = orders;
        _authorization = authorization;
    }

    public async Task<Result<List<OrderDto>>> Handle(GetOrdersByProjectQuery request, CancellationToken cancellationToken)
    {
        if (!await _authorization.CanManageProjectAsync(request.CallerUserId, request.ProjectId))
            return Result.Fail<List<OrderDto>>(ResultErrors.Forbidden());

        var orders = await _orders.GetByProjectAsync(request.ProjectId);
        return Result.Ok(orders.Adapt<List<OrderDto>>());
    }
}
