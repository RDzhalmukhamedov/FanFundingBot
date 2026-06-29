namespace FanFundingBot.Bot.Abstractions;

public interface INotificationService
{
    Task NotifyProjectStatusUpdateAsync(Guid projectId, string projectName, string newStatus);
    Task BroadcastToProjectAsync(Guid projectId, string text);
    Task NotifyQuestionAnsweredAsync(long userId, string questionText, string answer);
}
