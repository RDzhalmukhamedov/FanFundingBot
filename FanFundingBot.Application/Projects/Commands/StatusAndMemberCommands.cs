using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record CreateProjectStatusCommand(
    long CallerUserId, string DisplayName, bool CanEditOrders)
    : IRequest<Result<int>>, IRequireGlobalAdmin;

public class CreateProjectStatusCommandValidator : AbstractValidator<CreateProjectStatusCommand>
{
    public CreateProjectStatusCommandValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(64);
    }
}

public class CreateProjectStatusCommandHandler : IRequestHandler<CreateProjectStatusCommand, Result<int>>
{
    private readonly IProjectStatusRepository _statuses;

    public CreateProjectStatusCommandHandler(IProjectStatusRepository statuses)
    {
        _statuses = statuses;
    }

    public async Task<Result<int>> Handle(CreateProjectStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _statuses.GetMaxOrderAsync() + 1;
        var entry = new ProjectStatus(request.DisplayName, order, request.CanEditOrders);
        await _statuses.AddAsync(entry);

        return Result.Ok(entry.Id);
    }
}

public sealed record AssignManagerCommand(
    long CallerUserId, Guid ProjectId, long TargetUserId, ProjectRole Role)
    : IRequest<Result>, IRequireProjectAccess;

public class AssignManagerCommandValidator : AbstractValidator<AssignManagerCommand>
{
    public AssignManagerCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.TargetUserId).GreaterThan(0);
        RuleFor(x => x.Role).IsInEnum();
    }
}

public class AssignManagerCommandHandler : IRequestHandler<AssignManagerCommand, Result>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly IAuthorizationService _authorization;

    public AssignManagerCommandHandler(
        IProjectRepository projects,
        IUserRepository users,
        IAuthorizationService authorization)
    {
        _projects = projects;
        _users = users;
        _authorization = authorization;
    }

    public async Task<Result> Handle(AssignManagerCommand request, CancellationToken cancellationToken)
    {
        if (!await _authorization.CanAssignManagerAsync(request.CallerUserId, request.ProjectId))
            return Result.Fail(ResultErrors.Forbidden());

        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail(ResultErrors.ProjectNotFound());

        var target = await _users.GetByIdAsync(request.TargetUserId);
        if (target is null)
            return Result.Fail(ResultErrors.UserNotFound());

        project.AddMember(request.TargetUserId, request.Role);
        await _projects.UpdateMembersAsync(project);

        return Result.Ok();
    }
}
