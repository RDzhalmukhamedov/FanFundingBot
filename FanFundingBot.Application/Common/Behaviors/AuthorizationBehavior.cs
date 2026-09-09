using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Common.Errors;
using FanFundingBot.Domain.Interfaces;
using FluentResults;

namespace FanFundingBot.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
    where TResponse : ResultBase, new()
{
    private readonly ICurrentUserProvider _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IProjectRepository _projects;

    public AuthorizationBehavior(
        ICurrentUserProvider currentUser,
        IAuthorizationService authorization,
        IProjectRepository projects)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _projects = projects;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        MediatR.RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var userId = _currentUser.GetCurrentUserId();

        bool allowed = request switch
        {
            IRequireGlobalAdmin => await _authorization.CanCreateProjectAsync(userId),
            IRequireProjectAccess pa => await _authorization.CanManageProjectAsync(userId, pa.ProjectId),
            IRequireAddonAccess aa => await CheckAddonAsync(userId, aa.AddonId),
            _ => true
        };

        if (!allowed)
            return Denied();

        return await next();
    }

    private async Task<bool> CheckAddonAsync(long userId, Guid addonId)
    {
        var projectId = await _projects.GetProjectIdByAddonAsync(addonId);
        if (projectId is null)
            return false;

        return await _authorization.CanManageProjectAsync(userId, projectId.Value);
    }

    private static TResponse Denied()
    {
        var response = new TResponse();
        response.Reasons.Add(ResultErrors.Forbidden());
        return response;
    }
}
