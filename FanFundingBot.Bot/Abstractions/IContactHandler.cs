using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IContactHandler
{
    Task HandleContactAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
}
