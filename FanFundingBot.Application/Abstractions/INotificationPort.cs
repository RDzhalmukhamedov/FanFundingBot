namespace FanFundingBot.Application.Abstractions;

public interface INotificationPort
{
    Task NotifyProjectStatusChangedAsync(
        IReadOnlyList<long> userIds, string projectName, string newStatus, CancellationToken ct);

    Task BroadcastToProjectAsync(
        IReadOnlyList<long> userIds, string message, CancellationToken ct);

    Task NotifyQuestionAnsweredAsync(
        long userId, string questionText, string answer, CancellationToken ct);
}
