using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface ITextInputHandler
{
    Task HandleTextInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
}
