using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record BroadcastCommand(long CallerUserId, Guid ProjectId, string Message)
    : IRequest<Result>, IRequireProjectAccess;

public class BroadcastCommandValidator : AbstractValidator<BroadcastCommand>
{
    public BroadcastCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Message).NotEmpty().MaximumLength(4000);
    }
}

public class BroadcastCommandHandler : IRequestHandler<BroadcastCommand, Result>
{
    private readonly IProjectRepository _projects;
    private readonly IUserRepository _users;
    private readonly INotificationPort _notifications;

    public BroadcastCommandHandler(
        IProjectRepository projects,
        IUserRepository users,
        INotificationPort notifications)
    {
        _projects = projects;
        _users = users;
        _notifications = notifications;
    }

    public async Task<Result> Handle(BroadcastCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail("Проект не найден.");

        var subscribers = await _users.GetSubscribedToProjectAsync(request.ProjectId);
        await _notifications.BroadcastToProjectAsync(
            subscribers.Select(u => u.Id).ToList(),
            request.Message,
            cancellationToken);

        return Result.Ok();
    }
}
