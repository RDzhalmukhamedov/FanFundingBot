using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class AdminStatusCallbackHandler : IAdminStatusCallbackHandler
{
    private readonly IAdminService _adminService;
    private readonly MainMenuHelper _menuHelper;

    public AdminStatusCallbackHandler(IAdminService adminService, MainMenuHelper menuHelper)
    {
        _adminService = adminService;
        _menuHelper = menuHelper;
    }

    public async Task HandleAdminStatusAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (_, args) = CallbackData.Parse(data);
        var projectId = Guid.Parse(args[0]);
        var statusId = int.Parse(args[1]);

        var project = await _adminService.GetProjectAsync(projectId);
        if (project == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Проект не найден.", cancellationToken: ct);
            return;
        }

        var statuses = await _adminService.GetAllStatusesAsync();
        var currentStatus = statuses.FirstOrDefault(s => s.Id == project.StatusId);
        var newStatus = statuses.FirstOrDefault(s => s.Id == statusId);

        var keyboard = ConfirmationKeyboard.CreateStatusConfirmation(projectId, statusId);

        var text = $"⚠️ <b>Сменить статус проекта «{project.Name}»?</b>\n\n" +
                   $"Текущий статус: {currentStatus?.DisplayName ?? "❓ Неизвестно"}\n" +
                   $"Новый статус: {newStatus?.DisplayName ?? "❓ Неизвестно"}\n\n" +
                   $"Подтвердите действие:";

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminStatusConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (_, args) = CallbackData.Parse(data);
        var projectId = Guid.Parse(args[0]);
        var statusId = int.Parse(args[1]);

        var result = await _adminService.UpdateProjectStatusAsync(projectId, statusId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleAdminStatusCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);

        var project = await _adminService.GetProjectAsync(projectId);
        if (project == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Проект не найден.", cancellationToken: ct);
            return;
        }

        var statuses = await _adminService.GetAllStatusesAsync();
        var (keyboard, _) = StatusSelectionKeyboard.Create(statuses, projectId, 1);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            $"Выберите новый статус для проекта «{project.Name}»:",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminStatusPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (_, args) = CallbackData.Parse(data);
        var pageStr = args[1];
        if (pageStr == "nop") return;

        var projectId = Guid.Parse(args[0]);
        var page = int.Parse(pageStr);

        var project = await _adminService.GetProjectAsync(projectId);
        if (project == null) return;

        var statuses = await _adminService.GetAllStatusesAsync();
        var (keyboard, _) = StatusSelectionKeyboard.Create(statuses, projectId, page);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            $"Выберите новый статус для проекта «{project.Name}»:",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}