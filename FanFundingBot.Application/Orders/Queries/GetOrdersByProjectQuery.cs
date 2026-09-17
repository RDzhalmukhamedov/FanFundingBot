using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Application.Common.Models;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Orders.Queries;

public sealed record GetOrdersByProjectQuery(Guid ProjectId, long CallerUserId, int Page = 1, int PageSize = 20)
    : IRequest<Result<PagedResult<OrderDto>>>;

public class GetOrdersByProjectQueryHandler : IRequestHandler<GetOrdersByProjectQuery, Result<PagedResult<OrderDto>>>
{
    private readonly IOrderRepository _orders;
    private readonly IAuthorizationService _authorization;

    public GetOrdersByProjectQueryHandler(IOrderRepository orders, IAuthorizationService authorization)
    {
        _orders = orders;
        _authorization = authorization;
    }

    public async Task<Result<PagedResult<OrderDto>>> Handle(GetOrdersByProjectQuery request, CancellationToken cancellationToken)
    {
        if (!await _authorization.CanManageProjectAsync(request.CallerUserId, request.ProjectId))
            return Result.Fail<PagedResult<OrderDto>>(ResultErrors.Forbidden());

        var page = Math.Max(request.Page, 1);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var (items, total) = await _orders.GetByProjectPagedAsync(request.ProjectId, page, pageSize);
        return Result.Ok(new PagedResult<OrderDto>(items.Adapt<List<OrderDto>>(), total, page, pageSize));
    }
}
