using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IOrderTextHandler
{
    Task HandlePhoneInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandleAddressInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
}