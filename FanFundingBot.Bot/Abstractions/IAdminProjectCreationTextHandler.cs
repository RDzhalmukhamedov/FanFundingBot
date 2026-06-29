using Telegram.Bot;
using Telegram.Bot.Types;
using FanFundingBot.Bot.Models;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminProjectCreationTextHandler
{
    Task HandleCreateProjectNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleCreateProjectDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleCreateAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleCreateAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleCreateAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
}