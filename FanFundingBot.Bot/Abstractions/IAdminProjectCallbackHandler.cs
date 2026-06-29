using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IAdminProjectCallbackHandler
{
    Task HandleAdminCreateProjectConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleAdminEditProjectAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminEditProjectNameAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminEditProjectDescAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminAddAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminEditAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminEditAddonDescAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminEditAddonPriceAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminDeleteAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminDeleteAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminDeleteAddonCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminAddCreateAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleAdminAddStatusAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAdminAddStatusCanEditAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
}