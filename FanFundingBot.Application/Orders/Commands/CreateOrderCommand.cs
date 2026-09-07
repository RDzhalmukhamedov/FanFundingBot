using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Extensions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Orders.Commands;

public sealed record CreateOrderCommand(
    long CallerUserId,
    Guid ProjectId,
    Dictionary<Guid, int> AddonQuantities,
    string Phone,
    DeliveryMethod DeliveryMethod,
    string? Address) : IRequest<Result<Guid>>;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
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

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;
    private readonly IOrderRepository _orders;

    public CreateOrderCommandHandler(
        IUserRepository users,
        IProjectRepository projects,
        IOrderRepository orders)
    {
        _users = users;
        _projects = projects;
        _orders = orders;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.CallerUserId);
        if (user is null)
            return Result.Fail<Guid>("Пользователь не найден.");

        var duplicates = await _orders.GetByProjectAndUserAsync(request.ProjectId, request.CallerUserId);
        if (duplicates.Any(o => o.Status != OrderStatus.Cancelled))
            return Result.Fail<Guid>("У вас уже есть заказ на этот проект.");

        if (user.Phone != request.Phone)
        {
            user.SetPhone(request.Phone);
            await _users.UpdateAsync(user);
        }

        var project = await _projects.GetByIdWithAddonsAsync(request.ProjectId);
        if (project is null)
            return Result.Fail<Guid>("Проект не найден.");

        if (!project.Status.CanEditOrders)
            return Result.Fail<Guid>("Проект недоступен для заказа.");

        var draft = user.StartOrdering(project);

        foreach (var (addonId, quantity) in request.AddonQuantities)
        {
            var addon = project.Addons.FirstOrDefault(a => a.Id == addonId);
            if (addon is null)
                return Result.Fail<Guid>($"Дополнение {addonId} не найдено.");

            draft.AddAddon(addon, quantity);
        }

        draft.SetPhoneNumber(request.Phone);
        draft.SetDeliveryMethod(request.DeliveryMethod);
        if (request.DeliveryMethod == DeliveryMethod.Delivery)
            draft.SetShippingAddress(request.Address);

        var formed = draft.Form();
        await _orders.AddAsync(formed);

        return Result.Ok(formed.Id);
    }
}
