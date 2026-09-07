using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Interfaces;
using FluentResults;

namespace FanFundingBot.Application.Common.Behaviors;

public class AuthorizationBehavior<TRequest, TResponse> : MediatR.IPipelineBehavior<TRequest, TResponse>
    where TRequest : MediatR.IRequest<TResponse>
    where TResponse : ResultBase, new()
{
    private readonly ICurrentUserProvider _currentUser;
    private readonly IAuthorizationService _authorization;
    private readonly IAddonRepository _addons;

    public AuthorizationBehavior(
        ICurrentUserProvider currentUser,
        IAuthorizationService authorization,
        IAddonRepository addons)
    {
        _currentUser = currentUser;
        _authorization = authorization;
        _addons = addons;
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
        var projectId = await _addons.GetProjectIdByAddonAsync(addonId);
        if (projectId is null)
            return false;

        return await _authorization.CanManageProjectAsync(userId, projectId.Value);
    }

    private static TResponse Denied()
    {
        var response = new TResponse();
        response.Reasons.Add(new Error("Нет прав."));
        return response;
    }
}
