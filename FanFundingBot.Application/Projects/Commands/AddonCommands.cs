using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record CreateAddonCommand(
    long CallerUserId, Guid ProjectId, string Name, string Description, decimal Price)
    : IRequest<Result<Guid>>, IRequireProjectAccess;

public class CreateAddonCommandValidator : AbstractValidator<CreateAddonCommand>
{
    public CreateAddonCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Description).MaximumLength(2048);
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class CreateAddonCommandHandler : IRequestHandler<CreateAddonCommand, Result<Guid>>
{
    private readonly IProjectRepository _projects;

    public CreateAddonCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result<Guid>> Handle(CreateAddonCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdWithAddonsAsync(request.ProjectId);
        if (project is null)
            return Result.Fail<Guid>("Проект не найден.");

        var addon = new Addon();
        addon.SetName(request.Name);
        addon.SetDescription(request.Description);
        addon.SetPrice(request.Price);
        addon.FinishEdit();

        project.StartEdit();
        project.AddAddon(addon);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok(addon.Id);
    }
}

public sealed record UpdateAddonNameCommand(long CallerUserId, Guid AddonId, string Name)
    : IRequest<Result>, IRequireAddonAccess;

public sealed record UpdateAddonDescriptionCommand(long CallerUserId, Guid AddonId, string Description)
    : IRequest<Result>, IRequireAddonAccess;

public sealed record UpdateAddonPriceCommand(long CallerUserId, Guid AddonId, decimal Price)
    : IRequest<Result>, IRequireAddonAccess;

public class UpdateAddonNameCommandValidator : AbstractValidator<UpdateAddonNameCommand>
{
    public UpdateAddonNameCommandValidator()
    {
        RuleFor(x => x.AddonId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public class UpdateAddonDescriptionCommandValidator : AbstractValidator<UpdateAddonDescriptionCommand>
{
    public UpdateAddonDescriptionCommandValidator()
    {
        RuleFor(x => x.AddonId).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2048);
    }
}

public class UpdateAddonPriceCommandValidator : AbstractValidator<UpdateAddonPriceCommand>
{
    public UpdateAddonPriceCommandValidator()
    {
        RuleFor(x => x.AddonId).NotEmpty();
        RuleFor(x => x.Price).GreaterThanOrEqualTo(0);
    }
}

public class UpdateAddonNameCommandHandler : IRequestHandler<UpdateAddonNameCommand, Result>
{
    private readonly IAddonRepository _addons;

    public UpdateAddonNameCommandHandler(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result> Handle(UpdateAddonNameCommand request, CancellationToken cancellationToken)
    {
        var addon = await _addons.GetByIdAsync(request.AddonId);
        if (addon is null)
            return Result.Fail("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetName(request.Name);
        addon.FinishEdit();

        await _addons.UpdateAsync(addon);

        return Result.Ok();
    }
}

public class UpdateAddonDescriptionCommandHandler : IRequestHandler<UpdateAddonDescriptionCommand, Result>
{
    private readonly IAddonRepository _addons;

    public UpdateAddonDescriptionCommandHandler(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result> Handle(UpdateAddonDescriptionCommand request, CancellationToken cancellationToken)
    {
        var addon = await _addons.GetByIdAsync(request.AddonId);
        if (addon is null)
            return Result.Fail("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetDescription(request.Description);
        addon.FinishEdit();

        await _addons.UpdateAsync(addon);

        return Result.Ok();
    }
}

public class UpdateAddonPriceCommandHandler : IRequestHandler<UpdateAddonPriceCommand, Result>
{
    private readonly IAddonRepository _addons;

    public UpdateAddonPriceCommandHandler(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result> Handle(UpdateAddonPriceCommand request, CancellationToken cancellationToken)
    {
        var addon = await _addons.GetByIdAsync(request.AddonId);
        if (addon is null)
            return Result.Fail("Дополнение не найдено.");

        addon.StartEdit();
        addon.SetPrice(request.Price);
        addon.FinishEdit();

        await _addons.UpdateAsync(addon);

        return Result.Ok();
    }
}

public sealed record DeleteAddonCommand(long CallerUserId, Guid AddonId)
    : IRequest<Result>, IRequireAddonAccess;

public class DeleteAddonCommandValidator : AbstractValidator<DeleteAddonCommand>
{
    public DeleteAddonCommandValidator()
    {
        RuleFor(x => x.AddonId).NotEmpty();
    }
}

public class DeleteAddonCommandHandler : IRequestHandler<DeleteAddonCommand, Result>
{
    private readonly IAddonRepository _addons;

    public DeleteAddonCommandHandler(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result> Handle(DeleteAddonCommand request, CancellationToken cancellationToken)
    {
        var addon = await _addons.GetByIdAsync(request.AddonId);
        if (addon is null)
            return Result.Fail("Дополнение не найдено.");

        if (await _addons.HasOrdersAsync(request.AddonId))
            return Result.Fail("Нельзя удалить дополнение, так как оно есть в заказах.");

        await _addons.DeleteAsync(request.AddonId);

        return Result.Ok();
    }
}
