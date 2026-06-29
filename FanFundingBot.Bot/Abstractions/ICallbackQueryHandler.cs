using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface ICallbackQueryHandler
{
    Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery query, CancellationToken ct);
}
