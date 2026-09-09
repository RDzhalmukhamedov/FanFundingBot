using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
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
            return Result.Fail<Guid>(ResultErrors.ProjectNotFound());

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
    private readonly IProjectRepository _projects;

    public UpdateAddonNameCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result> Handle(UpdateAddonNameCommand request, CancellationToken cancellationToken)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail(ResultErrors.AddonNotFound());

        var project = await _projects.GetByIdWithAddonsAsync(projectId.Value);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        project.StartEdit();
        project.UpdateAddonName(request.AddonId, request.Name);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok();
    }
}

public class UpdateAddonDescriptionCommandHandler : IRequestHandler<UpdateAddonDescriptionCommand, Result>
{
    private readonly IProjectRepository _projects;

    public UpdateAddonDescriptionCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result> Handle(UpdateAddonDescriptionCommand request, CancellationToken cancellationToken)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail(ResultErrors.AddonNotFound());

        var project = await _projects.GetByIdWithAddonsAsync(projectId.Value);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        project.StartEdit();
        project.UpdateAddonDescription(request.AddonId, request.Description);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok();
    }
}

public class UpdateAddonPriceCommandHandler : IRequestHandler<UpdateAddonPriceCommand, Result>
{
    private readonly IProjectRepository _projects;

    public UpdateAddonPriceCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result> Handle(UpdateAddonPriceCommand request, CancellationToken cancellationToken)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail(ResultErrors.AddonNotFound());

        var project = await _projects.GetByIdWithAddonsAsync(projectId.Value);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        project.StartEdit();
        project.UpdateAddonPrice(request.AddonId, request.Price);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

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
    private readonly IProjectRepository _projects;
    private readonly IAddonUsageReader _usageReader;

    public DeleteAddonCommandHandler(IProjectRepository projects, IAddonUsageReader usageReader)
    {
        _projects = projects;
        _usageReader = usageReader;
    }

    public async Task<Result> Handle(DeleteAddonCommand request, CancellationToken cancellationToken)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail(ResultErrors.AddonNotFound());

        var project = await _projects.GetByIdWithAddonsAsync(projectId.Value);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        var hasOrders = await _usageReader.HasOrdersAsync(request.AddonId, cancellationToken);

        project.StartEdit();
        project.RemoveAddon(request.AddonId, hasOrders);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok();
    }
}
