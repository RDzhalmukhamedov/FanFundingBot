using FanFundingBot.Domain.Entities;

namespace FanFundingBot.Bot.Abstractions;

[System.Obsolete("Используйте команды FanFundingBot.Application. Будет удалено в Фазе 4.")]
public interface IAdminService
{
    Task<bool> IsAdminAsync(long userId);
    Task EnsureAdminRoleAsync(long userId);
    Task<string> UpdateProjectStatusAsync(Guid projectId, int statusId);
    Task<string> BroadcastMessageAsync(Guid projectId, string message);
    Task<List<Question>> GetUnansweredQuestionsAsync();
    Task<string> AnswerQuestionAsync(Guid questionId, string answer, long adminUserId);
    Task<Project?> GetProjectAsync(Guid projectId);
    Task<string> CreateProjectAsync(long callerUserId, string name, string description, List<(string Name, string Description, decimal Price)>? addons);
    Task<string> DeleteAddonAsync(Guid addonId);
    Task<string> UpdateProjectNameAsync(Guid projectId, string name);
    Task<string> UpdateProjectDescriptionAsync(Guid projectId, string description);
    Task<string> UpdateAddonNameAsync(Guid addonId, string name);
    Task<string> UpdateAddonDescriptionAsync(Guid addonId, string description);
    Task<string> UpdateAddonPriceAsync(Guid addonId, decimal price);
    Task<string> CreateAddonAsync(Guid projectId, string name, string description, decimal price);
    Task<string> CreateProjectStatusAsync(string displayName, int order, bool canEdit);
    Task<List<ProjectStatus>> GetAllStatusesAsync();
}
