using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminCallbackQueryHandler : IAdminCallbackQueryHandler
{
    private readonly IUserStateService _stateService;
    private readonly IAdminService _adminService;
    private readonly IAddonRepository _addonRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly MainMenuHelper _menuHelper;
    private readonly IProjectEditFormBuilder _editFormBuilder;

    public AdminCallbackQueryHandler(
        IUserStateService stateService,
        IAdminService adminService,
        IAddonRepository addonRepository,
        IProjectRepository projectRepository,
        MainMenuHelper menuHelper,
        IProjectEditFormBuilder editFormBuilder)
    {
        _stateService = stateService;
        _adminService = adminService;
        _addonRepository = addonRepository;
        _projectRepository = projectRepository;
        _menuHelper = menuHelper;
        _editFormBuilder = editFormBuilder;
    }

    // --- Admin: Project Status ---

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
        var currentStatus = statuses.FirstOrDefault(s => s.Id == project.Status.Id);
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

    // --- Admin: Questions ---

    public async Task HandleAdminSelectQuestionAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var questionId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "answer_question";
        session.PendingQuestionId = questionId;
        _stateService.Update(userId, session);

        var questions = await _adminService.GetUnansweredQuestionsAsync();
        var selected = questions.FirstOrDefault(q => q.Id == questionId);

        var usernameLine = selected?.User.Username != null
            ? $"👤 @{selected.User.Username}\n\n"
            : "";

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"📝 <b>Вопрос от {selected?.User.FirstName ?? "пользователя"}</b>\n\n" +
            usernameLine +
            $"{selected?.Text}\n\n" +
            "Введите ваш ответ:",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    // --- Admin: Broadcast ---

    public async Task HandleAdminBroadcastConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempBroadcastText == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Текст сообщения не найден. Попробуйте начать заново.",
                cancellationToken: ct);
            return;
        }

        var text = session.TempBroadcastText;
        session.PendingAction = null;
        session.TempBroadcastText = null;
        _stateService.Update(userId, session);

        var projects = await _projectRepository.GetAllAsync();
        if (projects.Count == 0)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Нет проектов для рассылки.",
                cancellationToken: ct);
            return;
        }

        var result = await _adminService.BroadcastMessageAsync(projects.First().Id, text);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleAdminCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "❌ Действие отменено.",
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    // --- Admin CRUD: Create Project ---

    public async Task HandleAdminCreateProjectConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempProjectName == null) return;

        var addons = session.TempAddons?
            .Select(a => (a.Name, a.Description, a.Price))
            .ToList();

        var result = await _adminService.CreateProjectAsync(
            userId, session.TempProjectName, session.TempProjectDescription ?? "", addons);

        _stateService.Remove(userId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            result,
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    // --- Admin CRUD: Edit Project ---

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

    // --- Admin CRUD: Addon management ---

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

    // --- Addon editing wizard ---

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

    // --- Delete addon with confirmation ---

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

    // --- Admin CRUD: Create-flow add-addon ---

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

    // --- Admin: Status page navigation ---

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

    // --- Admin: Add status ---

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
