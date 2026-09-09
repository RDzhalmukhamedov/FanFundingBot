using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
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
    private readonly IAddonUsageReader _usageReader;

    public GetProjectByIdQueryHandler(IProjectRepository projects, IAddonUsageReader usageReader)
    {
        _projects = projects;
        _usageReader = usageReader;
    }

    public async Task<Result<ProjectDto>> Handle(GetProjectByIdQuery request, CancellationToken cancellationToken)
    {
        var project = await _projects.GetByIdWithAddonsAsync(request.ProjectId);
        if (project is null)
            return Result.Fail<ProjectDto>(ResultErrors.ProjectNotFound());

        var dto = project.Adapt<ProjectDto>();

        if (dto.Addons.Count > 0)
        {
            var map = await _usageReader.GetHasOrdersMapAsync(dto.Addons.Select(a => a.Id), cancellationToken);
            var enriched = dto.Addons.Select(a => a with { HasOrders = map.TryGetValue(a.Id, out var v) && v }).ToList();
            dto = dto with { Addons = enriched };
        }

        return Result.Ok(dto);
    }
}

public sealed record GetProjectIdByAddonQuery(Guid AddonId) : IRequest<Result<Guid>>;

public class GetProjectIdByAddonQueryHandler : IRequestHandler<GetProjectIdByAddonQuery, Result<Guid>>
{
    private readonly IProjectRepository _projects;

    public GetProjectIdByAddonQueryHandler(IProjectRepository projects)
    {
        _projects = projects;
    }

    public async Task<Result<Guid>> Handle(GetProjectIdByAddonQuery request, CancellationToken cancellationToken)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(request.AddonId);
        if (projectId is null)
            return Result.Fail<Guid>(ResultErrors.AddonNotFound());

        return Result.Ok(projectId.Value);
    }
}
