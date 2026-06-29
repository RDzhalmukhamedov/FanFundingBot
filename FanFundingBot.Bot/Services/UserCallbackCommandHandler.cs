using FanFundingBot.Bot.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class UserCallbackCommandHandler : ICallbackCommandHandler
{
    private readonly IUserCallbackQueryHandler _userHandler;

    public UserCallbackCommandHandler(IUserCallbackQueryHandler userHandler)
    {
        _userHandler = userHandler;
    }

    public string[] Commands => new[]
    {
        "addon_toggle", "addon_page", "addon_confirm", "delivery",
        "order_confirm", "order_edit", "order_cancel",
        "addon_qty_inc", "addon_qty_dec",
        "order_edit_my", "order_delete_my", "order_delete_my_confirm", "order_delete_my_cancel",
        "back"
    };

    public bool RequireAdmin => false;

    public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (command, _) = Helpers.CallbackData.Parse(data);

        switch (command)
        {
            case "addon_toggle":
                await _userHandler.HandleAddonToggleAsync(botClient, query, userId, data, ct);
                break;
            case "addon_page":
                await _userHandler.HandleAddonPageAsync(botClient, query, userId, data, ct);
                break;
            case "addon_confirm":
                await _userHandler.HandleAddonConfirmAsync(botClient, query, userId, ct);
                break;
            case "delivery":
                await _userHandler.HandleDeliveryAsync(botClient, query, userId, data, ct);
                break;
            case "order_confirm":
                await _userHandler.HandleOrderConfirmAsync(botClient, query, userId, ct);
                break;
            case "order_edit":
                await _userHandler.HandleOrderEditAsync(botClient, query, userId, ct);
                break;
            case "order_cancel":
                await _userHandler.HandleOrderCancelAsync(botClient, query, userId, ct);
                break;
            case "addon_qty_inc":
                await _userHandler.HandleAddonQtyIncAsync(botClient, query, userId, data, ct);
                break;
            case "addon_qty_dec":
                await _userHandler.HandleAddonQtyDecAsync(botClient, query, userId, data, ct);
                break;
            case "order_edit_my":
                await _userHandler.HandleOrderEditMyAsync(botClient, query, userId, data, ct);
                break;
            case "order_delete_my":
                await _userHandler.HandleOrderDeleteMyAsync(botClient, query, userId, data, ct);
                break;
            case "order_delete_my_confirm":
                await _userHandler.HandleOrderDeleteMyConfirmAsync(botClient, query, userId, data, ct);
                break;
            case "order_delete_my_cancel":
                await _userHandler.HandleOrderDeleteMyCancelAsync(botClient, query, userId, data, ct);
                break;
            case "back":
                await _userHandler.HandleBackAsync(botClient, query, userId, data, ct);
                break;
        }
    }
}