using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record UpdateProjectStatusCommand(long CallerUserId, Guid ProjectId, int StatusId)
    : IRequest<Result<ProjectStatusUpdateResult>>, IRequireProjectAccess;

public sealed record ProjectStatusUpdateResult(string ProjectName, string StatusName);

public class UpdateProjectStatusCommandValidator : AbstractValidator<UpdateProjectStatusCommand>
{
    public UpdateProjectStatusCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.StatusId).GreaterThan(0);
    }
}

public class UpdateProjectStatusCommandHandler : IRequestHandler<UpdateProjectStatusCommand, Result<ProjectStatusUpdateResult>>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectStatusRepository _statuses;
    private readonly IUserRepository _users;
    private readonly INotificationPort _notifications;

    public UpdateProjectStatusCommandHandler(
        IProjectRepository projects,
        IProjectStatusRepository statuses,
        IUserRepository users,
        INotificationPort notifications)
    {
        _projects = projects;
        _statuses = statuses;
        _users = users;
        _notifications = notifications;
    }

    public async Task<Result<ProjectStatusUpdateResult>> Handle(UpdateProjectStatusCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail<ProjectStatusUpdateResult>("Проект не найден.");

        var status = await _statuses.GetByIdAsync(request.StatusId);
        if (status is null)
            return Result.Fail<ProjectStatusUpdateResult>("Статус не найден.");

        project.SetStatus(status);
        await _projects.UpdateAsync(project);

        var subscribers = await _users.GetSubscribedToProjectAsync(request.ProjectId);
        await _notifications.NotifyProjectStatusChangedAsync(
            subscribers.Select(u => u.Id).ToList(),
            project.Name,
            status.DisplayName,
            cancellationToken);

        return Result.Ok(new ProjectStatusUpdateResult(project.Name, status.DisplayName));
    }
}
