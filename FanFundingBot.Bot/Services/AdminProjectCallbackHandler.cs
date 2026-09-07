using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminProjectCallbackHandler : IAdminProjectCallbackHandler
{
    private readonly IUserStateService _stateService;
    private readonly IAdminService _adminService;
    private readonly IAddonRepository _addonRepository;
    private readonly IProjectEditFormBuilder _editFormBuilder;
    private readonly MainMenuHelper _menuHelper;

    public AdminProjectCallbackHandler(
        IUserStateService stateService,
        IAdminService adminService,
        IAddonRepository addonRepository,
        IProjectEditFormBuilder editFormBuilder,
        MainMenuHelper menuHelper)
    {
        _stateService = stateService;
        _adminService = adminService;
        _addonRepository = addonRepository;
        _editFormBuilder = editFormBuilder;
        _menuHelper = menuHelper;
    }

    public async Task HandleAdminCreateProjectConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempProjectName == null) return;

        var addons = session.TempAddons?
            .Select(a => (a.Name, a.Description, a.Price))
            .ToList();

        var result = await _adminService.CreateProjectAsync(
            session.TempProjectName, session.TempProjectDescription ?? "", addons);

        _stateService.Remove(userId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            result,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    private async Task ShowEditProjectFormAsync(ITelegramBotClient botClient, long chatId, int messageId, Guid projectId, long userId, CancellationToken ct, string? prependText = null)
    {
        try
        {
            var form = await _editFormBuilder.BuildAsync(projectId, prependText);
            var session = _stateService.GetOrCreate(userId);
            session.PendingAction = null;
            _stateService.Update(userId, session);

            await botClient.EditMessageText(
                chatId, messageId, form.Text,
                parseMode: ParseMode.Html, replyMarkup: form.Keyboard, cancellationToken: ct);
        }
        catch (InvalidOperationException)
        {
            await botClient.EditMessageText(chatId, messageId, "Проект не найден.", cancellationToken: ct);
        }
    }

    public async Task HandleAdminEditProjectAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);
        await ShowEditProjectFormAsync(botClient, query.Message!.Chat.Id, query.Message!.MessageId, projectId, userId, ct);
    }

    public async Task HandleAdminEditProjectNameAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = $"edit_project_name:{projectId:N}";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "📝 Введите новое название проекта:",
            cancellationToken: ct);
    }

    public async Task HandleAdminEditProjectDescAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = $"edit_project_desc:{projectId:N}";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "📝 Введите новое описание проекта:",
            cancellationToken: ct);
    }

    public async Task HandleAdminAddAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.TempProjectId = projectId;
        session.PendingAction = $"add_addon_name:{projectId:N}";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "➕ Введите название нового дополнения:",
            cancellationToken: ct);
    }

    public async Task HandleAdminEditAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.TempAddonId = addonId;
        session.PendingAction = $"wizard_addon_name:{addonId:N}";
        _stateService.Update(userId, session);

        var addon = await _addonRepository.GetByIdAsync(addonId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"✏️ <b>Редактирование дополнения «{addon?.Name}»</b>\n\n" +
            "Шаг 1 из 3. Введите новое название дополнения:",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    public async Task HandleAdminEditAddonDescAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = $"edit_addon_desc:{addonId:N}";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "📝 Введите новое описание дополнения:",
            cancellationToken: ct);
    }

    public async Task HandleAdminEditAddonPriceAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = $"edit_addon_price:{addonId:N}";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "💰 Введите новую цену дополнения:",
            cancellationToken: ct);
    }

    public async Task HandleAdminDeleteAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message!.MessageId, "Дополнение не найдено.", cancellationToken: ct);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да, удалить", CallbackData.Build("admin_del_addon_confirm", addonId)),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CallbackData.Build("admin_del_addon_cancel", await _addonRepository.GetProjectIdByAddonAsync(addonId) ?? Guid.Empty))
            }
        });

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"⚠️ <b>Удалить дополнение «{addon.Name}»?</b>\n\nЭто действие нельзя отменить.",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminDeleteAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var addon = await _addonRepository.GetByIdAsync(addonId);
        if (addon == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message!.MessageId, "Дополнение не найдено.", cancellationToken: ct);
            return;
        }

        var projectId = await _addonRepository.GetProjectIdByAddonAsync(addonId);
        if (projectId == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message!.MessageId, "Проект не найден.", cancellationToken: ct);
            return;
        }

        var result = await _adminService.DeleteAddonAsync(addonId);

        await ShowEditProjectFormAsync(botClient, query.Message!.Chat.Id, query.Message!.MessageId, projectId.Value, userId, ct, result);
    }

    public async Task HandleAdminDeleteAddonCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);
        await ShowEditProjectFormAsync(botClient, query.Message!.Chat.Id, query.Message!.MessageId, projectId, userId, ct);
    }

    public async Task HandleAdminAddCreateAddonAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "create_addon_name";
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "➕ Введите название дополнения:",
            cancellationToken: ct);
    }

    public async Task HandleAdminAddStatusAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var session = _stateService.GetOrCreate(userId);
        session.TempProjectId = CallbackData.GuidArg(data);
        session.PendingAction = "add_status_name";
        session.TempStatusName = null;
        session.TempStatusCanEdit = null;
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "➕ Введите отображаемое название нового статуса (например: «Черновик»):",
            cancellationToken: ct);
    }

    public async Task HandleAdminAddStatusCanEditAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempStatusName == null) return;

        var canEdit = CallbackData.FirstArg(data) == "yes";
        var displayName = session.TempStatusName;
        var statuses = await _adminService.GetAllStatusesAsync();
        var order = statuses.Count > 0 ? statuses.Max(s => s.Order) + 1 : 1;

        var result = await _adminService.CreateProjectStatusAsync(displayName, order, canEdit);

        session.PendingAction = null;
        session.TempProjectId = null;
        session.TempStatusName = null;
        session.TempStatusCanEdit = null;
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            result,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }
}