using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminCommandHandler
{
    Task HandleAdminUpdateStatusAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandleAdminBroadcastAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandleAdminAnswerQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandleAdminCreateProjectAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandleAdminEditProjectListAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
}