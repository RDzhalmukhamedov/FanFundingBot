using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Orders.Commands;

public sealed record SendDirectMessageCommand(long CallerUserId, Guid OrderId, string Text)
    : IRequest<Result>;

public class SendDirectMessageCommandValidator : AbstractValidator<SendDirectMessageCommand>
{
    public SendDirectMessageCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(4000);
    }
}

public class SendDirectMessageCommandHandler : IRequestHandler<SendDirectMessageCommand, Result>
{
    private readonly IOrderRepository _orders;
    private readonly IAuthorizationService _authorization;
    private readonly INotificationPort _notifications;

    public SendDirectMessageCommandHandler(
        IOrderRepository orders,
        IAuthorizationService authorization,
        INotificationPort notifications)
    {
        _orders = orders;
        _authorization = authorization;
        _notifications = notifications;
    }

    public async Task<Result> Handle(SendDirectMessageCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdWithAddonsAsync(request.OrderId);
        if (order is null)
            return Result.Fail(ResultErrors.OrderNotFound());

        if (!await _authorization.CanManageProjectAsync(request.CallerUserId, order.Project.Id))
            return Result.Fail(ResultErrors.Forbidden());

        await _notifications.SendDirectMessageAsync(order.User.Id, request.Text, cancellationToken);

        return Result.Ok();
    }
}
