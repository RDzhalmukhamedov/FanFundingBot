using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IQuestionTextHandler
{
    Task SaveQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct);
    Task SaveAnswerAsync(ITelegramBotClient botClient, Message message, Guid questionId, CancellationToken ct);
}