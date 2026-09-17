using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;

namespace FanFundingBot.Application.Services;

public class AuthorizationService : IAuthorizationService
{
    private readonly IUserRepository _users;
    private readonly IProjectRepository _projects;

    public AuthorizationService(IUserRepository users, IProjectRepository projects)
    {
        _users = users;
        _projects = projects;
    }

    public async Task<bool> CanCreateProjectAsync(long userId)
    {
        var user = await _users.GetByIdAsync(userId);
        return user is not null && user.Role == UserRole.Admin;
    }

    public async Task<bool> CanManageProjectAsync(long userId, Guid projectId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is not null && user.Role == UserRole.Admin)
            return true;

        var project = await _projects.GetByIdAsync(projectId);
        return project?.IsMember(userId) ?? false;
    }

    public async Task<bool> CanAssignManagerAsync(long userId, Guid projectId)
    {
        var user = await _users.GetByIdAsync(userId);
        if (user is not null && user.Role == UserRole.Admin)
            return true;

        var project = await _projects.GetByIdAsync(projectId);
        return project?.GetMemberRole(userId) == ProjectRole.ProjectAdmin;
    }
}
