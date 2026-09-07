using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Projects.Queries;

public sealed record GetProjectByIdQuery(Guid ProjectId) : IRequest<Result<ProjectDto>>;

public class GetProjectByIdQueryHandler : IRequestHandler<GetProjectByIdQuery, Result<ProjectDto>>
{
    private readonly IProjectRepository _projects;

    public GetProjectByIdQueryHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdWithAddonsAsync(request.ProjectId);
        if (project is null)
            return Result.Fail<ProjectDto>("Проект не найден.");

        return Result.Ok(project.Adapt<ProjectDto>());
    }
}

public sealed record GetProjectIdByAddonQuery(Guid AddonId) : IRequest<Result<Guid>>;

public class GetProjectIdByAddonQueryHandler : IRequestHandler<GetProjectIdByAddonQuery, Result<Guid>>
{
    private readonly IAddonRepository _addons;

    public GetProjectIdByAddonQueryHandler(IAddonRepository addons)
    {
        _addons = addons;
    }

    public async Task<Result<Guid>> Handle(GetProjectIdByAddonQuery request, CancellationToken cancellationToken)
    {
        var projectId = await _addons.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail<Guid>("Дополнение не найдено.");

        return Result.Ok(projectId.Value);
    }
}
