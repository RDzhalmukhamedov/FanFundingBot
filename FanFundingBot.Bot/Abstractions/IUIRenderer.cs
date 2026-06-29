using FanFundingBot.Core.Entities;
using Telegram.Bot;

namespace FanFundingBot.Bot.Abstractions;

public interface IUIRenderer
{
    Task ShowConfirmationAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct);
    Task SendAddonSelectionAsync(ITelegramBotClient botClient, long chatId, long userId, Project project, CancellationToken ct);
    Task HandleMyOrderByOrderAsync(ITelegramBotClient botClient, long chatId, Order order, CancellationToken ct);
}
