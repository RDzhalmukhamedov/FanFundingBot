namespace FanFundingBot.Application.Abstractions;

public interface IAuthorizationService
{
    Task<bool> CanCreateProjectAsync(long userId);
    Task<bool> CanManageProjectAsync(long userId, Guid projectId);
    Task<bool> CanAssignManagerAsync(long userId, Guid projectId);
}
