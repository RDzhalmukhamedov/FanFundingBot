using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminQuestionCallbackHandler
{
    Task HandleAdminSelectQuestionAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminBroadcastConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleAdminCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
}