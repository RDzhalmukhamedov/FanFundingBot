using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Projects.Queries;

public sealed record GetProjectsQuery : IRequest<Result<List<ProjectDto>>>;

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, Result<List<ProjectDto>>>
{
    private readonly IProjectRepository _projects;

    public GetProjectsQueryHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result<List<ProjectDto>>> Handle(GetProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _projects.GetAllWithAddonsAsync();
        return Result.Ok(projects.Adapt<List<ProjectDto>>());
    }
}

public sealed record GetProjectStatusesQuery : IRequest<Result<List<ProjectStatusDto>>>;

public class GetProjectStatusesQueryHandler : IRequestHandler<GetProjectStatusesQuery, Result<List<ProjectStatusDto>>>
{
    private readonly IProjectStatusRepository _statuses;

    public GetProjectStatusesQueryHandler(IProjectStatusRepository statuses)
    {
        _statuses = statuses;
    }

    public async Task<Result<List<ProjectStatusDto>>> Handle(GetProjectStatusesQuery request, CancellationToken cancellationToken)
    {
        var statuses = await _statuses.GetAllAsync();
        return Result.Ok(statuses.Adapt<List<ProjectStatusDto>>());
    }
}
