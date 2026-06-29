using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IBroadcastTextHandler
{
    Task SendBroadcastAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
}