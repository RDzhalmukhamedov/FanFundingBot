using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Extensions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Orders.Commands;

public sealed record UpdateOrderCommand(
    Guid OrderId,
    long CallerUserId,
    Dictionary<Guid, int> AddonQuantities,
    string Phone,
    DeliveryMethod DeliveryMethod,
    string? Address) : IRequest<Result>;

public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.AddonQuantities).NotEmpty();
        RuleForEach(x => x.AddonQuantities)
            .Must(kv => kv.Value is >= 1 and <= 100)
            .WithMessage("Количество должно быть от 1 до 100.");
        RuleFor(x => x.Phone)
            .Must(p => p.IsValidPhone())
            .WithMessage("Некорректный номер телефона.");
        RuleFor(x => x.Address)
            .Must(a => a.IsValidAddress())
            .When(x => x.DeliveryMethod == DeliveryMethod.Delivery)
            .WithMessage("Некорректный адрес доставки.");
    }
}

public class UpdateOrderCommandHandler : IRequestHandler<UpdateOrderCommand, Result>
{
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;

    public UpdateOrderCommandHandler(IProjectRepository projects, IOrderRepository orders)
    {
        _projects = projects;
        _orders = orders;
    }

    public async Task<Result> Handle(UpdateOrderCommand request, CancellationToken cancellationToken)
    {
        var order = await _orders.GetByIdWithAddonsAsync(request.OrderId);
        if (order is null)
            return Result.Fail("Заказ не найден.");

        if (order.User.Id != request.CallerUserId)
            return Result.Fail("Нет прав.");

        var project = await _projects.GetByIdWithAddonsAsync(order.Project.Id);
        if (project is null)
            return Result.Fail("Проект не найден.");

        if (!project.Status.CanEditOrders)
            return Result.Fail("Проект недоступен для заказа.");

        DraftOrder draft = order is DraftOrder d ? d : order.Edit();

        foreach (var position in draft.OrderAddons.ToList())
            draft.DeleteAddon(position.Addon);

        var addonsById = project.Addons.ToDictionary(a => a.Id);
        foreach (var (addonId, quantity) in request.AddonQuantities)
        {
            if (!addonsById.TryGetValue(addonId, out var addon))
                return Result.Fail($"Дополнение {addonId} не найдено.");

            draft.AddAddon(addon, quantity);
        }

        draft.SetPhoneNumber(request.Phone);
        draft.SetDeliveryMethod(request.DeliveryMethod);
        draft.SetShippingAddress(request.DeliveryMethod == DeliveryMethod.Delivery ? request.Address : null);

        var formed = draft.Form();
        await _orders.UpdateAsync(formed);

        return Result.Ok();
    }
}
