using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface ICommandHandler
{
    Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task HandlePhoneContactAsync(ITelegramBotClient botClient, Message message, string phone, CancellationToken ct);
}
