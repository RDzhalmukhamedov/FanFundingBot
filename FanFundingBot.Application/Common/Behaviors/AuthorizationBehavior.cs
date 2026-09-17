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

        if (request is IRequireAddonAccess aa)
        {
            var projectId = await _projects.GetProjectIdByAddonAsync(aa.AddonId);
            if (projectId is null)
            {
                var notFound = new TResponse();
                notFound.Reasons.Add(ResultErrors.AddonNotFound());
                return notFound;
            }

            var allowed = await _authorization.CanManageProjectAsync(userId, projectId.Value);
            if (!allowed)
                return Denied();

            return await next();
        }

        bool allowedGeneral = request switch
        {
            IRequireGlobalAdmin => await _authorization.CanCreateProjectAsync(userId),
            IRequireProjectAccess pa => await _authorization.CanManageProjectAsync(userId, pa.ProjectId),
            _ => true
        };

        if (!allowedGeneral)
            return Denied();

        return await next();
    }

    private static TResponse Denied()
    {
        var response = new TResponse();
        response.Reasons.Add(ResultErrors.Forbidden());
        return response;
    }
}
