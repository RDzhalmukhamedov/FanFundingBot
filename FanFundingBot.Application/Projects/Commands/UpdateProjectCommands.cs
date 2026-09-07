using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using FluentValidation;
using MediatR;

namespace FanFundingBot.Application.Projects.Commands;

public sealed record RenameProjectCommand(long CallerUserId, Guid ProjectId, string Name)
    : IRequest<Result>, IRequireProjectAccess;

public class RenameProjectCommandValidator : AbstractValidator<RenameProjectCommand>
{
    public RenameProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(256);
    }
}

public class RenameProjectCommandHandler : IRequestHandler<RenameProjectCommand, Result>
{
    private readonly IProjectRepository _projects;

    public RenameProjectCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result> Handle(RenameProjectCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail("Проект не найден.");

        project.StartEdit();
        project.SetName(request.Name);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok();
    }
}

public sealed record UpdateProjectDescriptionCommand(long CallerUserId, Guid ProjectId, string Description)
    : IRequest<Result>, IRequireProjectAccess;

public class UpdateProjectDescriptionCommandValidator : AbstractValidator<UpdateProjectDescriptionCommand>
{
    public UpdateProjectDescriptionCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2048);
    }
}

public class UpdateProjectDescriptionCommandHandler : IRequestHandler<UpdateProjectDescriptionCommand, Result>
{
    private readonly IProjectRepository _projects;

    public UpdateProjectDescriptionCommandHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result> Handle(UpdateProjectDescriptionCommand request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId);
        if (project is null)
            return Result.Fail("Проект не найден.");

        project.StartEdit();
        project.SetDescription(request.Description);
        project.FinishEdit();

        await _projects.UpdateAsync(project);

        return Result.Ok();
    }
}
