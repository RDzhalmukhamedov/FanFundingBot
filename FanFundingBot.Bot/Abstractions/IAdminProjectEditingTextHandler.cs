using Telegram.Bot;
using Telegram.Bot.Types;
using FanFundingBot.Bot.Models;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminProjectEditingTextHandler
{
    Task HandleEditProjectNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleEditProjectDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleEditAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleEditAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleEditAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleWizardAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleWizardAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleWizardAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleAddAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleAddAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleAddAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
    Task HandleAddStatusNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct);
}