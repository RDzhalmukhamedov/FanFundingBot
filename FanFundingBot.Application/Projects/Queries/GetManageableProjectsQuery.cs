using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Domain.Interfaces;
using FluentResults;
using Mapster;
using MediatR;

namespace FanFundingBot.Application.Projects.Queries;

public sealed record GetManageableProjectsQuery(long CallerUserId)
    : IRequest<Result<List<ProjectDto>>>;

public class GetManageableProjectsQueryHandler : IRequestHandler<GetManageableProjectsQuery, Result<List<ProjectDto>>>
{
    private readonly IProjectRepository _projects;
    private readonly IAuthorizationService _authorization;

    public GetManageableProjectsQueryHandler(IProjectRepository projects, IAuthorizationService authorization)
    {
        _projects = projects;
        _authorization = authorization;
    }

    public async Task<Result<List<ProjectDto>>> Handle(GetManageableProjectsQuery request, CancellationToken cancellationToken)
    {
        var projects = await _projects.GetAllWithAddonsAsync();

        // NB: N+1 допустим — проектов десятки, не тысячи (см. PLAN_BLAZOR.md).
        var manageable = new List<Domain.Entities.Project>();
        foreach (var project in projects)
        {
            if (await _authorization.CanManageProjectAsync(request.CallerUserId, project.Id))
                manageable.Add(project);
        }

        return Result.Ok(manageable.Adapt<List<ProjectDto>>());
    }
}
