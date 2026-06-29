using FanFundingBot.Bot.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class AdminCallbackCommandHandler : ICallbackCommandHandler
{
    private readonly IAdminStatusCallbackHandler _statusHandler;
    private readonly IAdminQuestionCallbackHandler _questionHandler;
    private readonly IAdminProjectCallbackHandler _projectHandler;

    public AdminCallbackCommandHandler(
        IAdminStatusCallbackHandler statusHandler,
        IAdminQuestionCallbackHandler questionHandler,
        IAdminProjectCallbackHandler projectHandler)
    {
        _statusHandler = statusHandler;
        _questionHandler = questionHandler;
        _projectHandler = projectHandler;
    }

    public string[] Commands => new[]
    {
        "admin_status", "admin_status_confirm", "admin_status_cancel", "admin_status_page",
        "admin_answer", "admin_broadcast_confirm", "admin_cancel",
        "admin_edit_project", "admin_edit_project_name", "admin_edit_project_desc",
        "admin_edit_add_addon", "admin_edit_addon", "admin_edit_addon_desc", "admin_edit_addon_price",
        "admin_del_addon", "admin_del_addon_confirm", "admin_del_addon_cancel",
        "admin_create_project_confirm", "admin_add_create_addon",
        "admin_add_status", "admin_add_status_can_edit"
    };

    public bool RequireAdmin => true;

    public async Task HandleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (command, _) = Helpers.CallbackData.Parse(data);

        switch (command)
        {
            case "admin_status":
                await _statusHandler.HandleAdminStatusAsync(botClient, query, userId, data, ct);
                break;
            case "admin_status_confirm":
                await _statusHandler.HandleAdminStatusConfirmAsync(botClient, query, userId, data, ct);
                break;
            case "admin_status_cancel":
                await _statusHandler.HandleAdminStatusCancelAsync(botClient, query, userId, data, ct);
                break;
            case "admin_status_page":
                await _statusHandler.HandleAdminStatusPageAsync(botClient, query, userId, data, ct);
                break;
            case "admin_answer":
                await _questionHandler.HandleAdminSelectQuestionAsync(botClient, query, userId, data, ct);
                break;
            case "admin_broadcast_confirm":
                await _questionHandler.HandleAdminBroadcastConfirmAsync(botClient, query, userId, ct);
                break;
            case "admin_cancel":
                await _questionHandler.HandleAdminCancelAsync(botClient, query, userId, ct);
                break;
            case "admin_edit_project":
                await _projectHandler.HandleAdminEditProjectAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_project_name":
                await _projectHandler.HandleAdminEditProjectNameAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_project_desc":
                await _projectHandler.HandleAdminEditProjectDescAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_add_addon":
                await _projectHandler.HandleAdminAddAddonAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_addon":
                await _projectHandler.HandleAdminEditAddonAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_addon_desc":
                await _projectHandler.HandleAdminEditAddonDescAsync(botClient, query, userId, data, ct);
                break;
            case "admin_edit_addon_price":
                await _projectHandler.HandleAdminEditAddonPriceAsync(botClient, query, userId, data, ct);
                break;
            case "admin_del_addon":
                await _projectHandler.HandleAdminDeleteAddonAsync(botClient, query, userId, data, ct);
                break;
            case "admin_del_addon_confirm":
                await _projectHandler.HandleAdminDeleteAddonConfirmAsync(botClient, query, userId, data, ct);
                break;
            case "admin_del_addon_cancel":
                await _projectHandler.HandleAdminDeleteAddonCancelAsync(botClient, query, userId, data, ct);
                break;
            case "admin_create_project_confirm":
                await _projectHandler.HandleAdminCreateProjectConfirmAsync(botClient, query, userId, ct);
                break;
            case "admin_add_create_addon":
                await _projectHandler.HandleAdminAddCreateAddonAsync(botClient, query, userId, ct);
                break;
            case "admin_add_status":
                await _projectHandler.HandleAdminAddStatusAsync(botClient, query, userId, data, ct);
                break;
            case "admin_add_status_can_edit":
                await _projectHandler.HandleAdminAddStatusCanEditAsync(botClient, query, userId, data, ct);
                break;
        }
    }
}