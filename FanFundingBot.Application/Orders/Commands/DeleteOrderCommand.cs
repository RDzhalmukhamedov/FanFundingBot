using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Orders.Commands;

public sealed record DeleteOrderCommand(Guid OrderId, long CallerUserId) : IRequest<Result>;

public class DeleteOrderCommandValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}

public class DeleteOrderCommandHandler : IRequestHandler<DeleteOrderCommand, Result>
{
    private readonly IOrderRepository _orders;

    public DeleteOrderCommandHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<Result> Handle(DeleteOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdWithAddonsAsync(request.OrderId);
        if (order is null)
            return Result.Ok();

        if (order.User.Id != request.CallerUserId)
            return Result.Fail("Нет прав.");

        order.AddDomainEvent(new OrderDeletedEvent(order.Id));
        await _orders.DeleteAsync(request.OrderId);

        return Result.Ok();
    }
}
