using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminStatusCallbackHandler
{
    Task HandleAdminStatusAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminStatusConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminStatusCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminStatusPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
}