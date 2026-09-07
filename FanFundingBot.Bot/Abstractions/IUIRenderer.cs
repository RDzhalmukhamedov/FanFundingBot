using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Bot.Abstractions;
using Telegram.Bot;

namespace FanFundingBot.Bot.Abstractions;

public interface IUIRenderer
{
    Task ShowConfirmationAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct);
    Task SendAddonSelectionAsync(ITelegramBotClient botClient, long chatId, long userId, ProjectDto project, CancellationToken ct);
    Task HandleMyOrderByOrderAsync(ITelegramBotClient botClient, long chatId, OrderDto order, CancellationToken ct);
}
