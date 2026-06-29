using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Abstractions;

public interface IUserCallbackQueryHandler
{
    Task HandleAddonToggleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAddonPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleDeliveryAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleOrderConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleOrderEditAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleOrderCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct);
    Task HandleAddonQtyIncAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleAddonQtyDecAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleOrderEditMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleOrderDeleteMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleOrderDeleteMyConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleOrderDeleteMyCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
    Task HandleBackAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct);
}
