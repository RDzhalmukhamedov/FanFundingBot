using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Orders.Commands;

public sealed record UpdateOrderStatusCommand(Guid OrderId, long CallerUserId, OrderStatus NewStatus)
    : IRequest<Result>;

public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.NewStatus).IsInEnum();
    }
}

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, Result>
{
    private readonly IOrderRepository _orders;
    private readonly IAuthorizationService _authorization;
    private readonly INotificationPort _notifications;

    public UpdateOrderStatusCommandHandler(
        IOrderRepository orders,
        IAuthorizationService authorization,
        INotificationPort notifications)
    {
        _orders = orders;
        _authorization = authorization;
        _notifications = notifications;
    }

    public async Task<Result> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdWithAddonsAsync(request.OrderId);
        if (order is null)
            return Result.Fail(ResultErrors.OrderNotFound());

        if (!await _authorization.CanManageProjectAsync(request.CallerUserId, order.Project.Id))
            return Result.Fail(ResultErrors.Forbidden());

        order.SetStatus(request.NewStatus);
        await _orders.UpdateAsync(order);

        await _notifications.NotifyOrderStatusChangedAsync(
            order.User.Id,
            order.Project.Name,
            request.NewStatus,
            cancellationToken);

        return Result.Ok();
    }
}
