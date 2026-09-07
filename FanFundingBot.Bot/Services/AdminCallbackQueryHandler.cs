using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminCallbackQueryHandler : IAdminCallbackQueryHandler
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;
    private readonly MainMenuHelper _menuHelper;
    private readonly IProjectEditFormBuilder _editFormBuilder;

    public AdminCallbackQueryHandler(
        IUserStateService stateService,
        IMediator mediator,
        MainMenuHelper menuHelper,
        IProjectEditFormBuilder editFormBuilder)
    {
        _stateService = stateService;
        _mediator = mediator;
        _menuHelper = menuHelper;
        _editFormBuilder = editFormBuilder;
    }

    public async Task HandleAdminStatusAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (_, args) = CallbackData.Parse(data);
        var projectId = Guid.Parse(args[0]);
        var statusId = int.Parse(args[1]);

        var projectResult = await _mediator.Send(new GetProjectByIdQuery(projectId), ct);
        if (projectResult.IsFailed)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Проект не найден.", cancellationToken: ct);
            return;
        }

        var statuses = await GetStatusesAsync(ct);
        var currentStatus = statuses.FirstOrDefault(s => s.Id == projectResult.Value.StatusId);
        var newStatus = statuses.FirstOrDefault(s => s.Id == statusId);

        var keyboard = ConfirmationKeyboard.CreateStatusConfirmation(projectId, statusId);

        var text = $"⚠️ <b>Сменить статус проекта «{projectResult.Value.Name}»?</b>\n\n" +
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

        var result = await _mediator.Send(new UpdateProjectStatusCommand(userId, projectId, statusId), ct);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result.IsSuccess
                ? $"✅ Статус проекта «{result.Value.ProjectName}» обновлён на «{result.Value.StatusName}».\n" +
                  "Уведомления отправлены подписанным пользователям."
                : result.Errors[0].Message,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleAdminStatusCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var projectId = CallbackData.GuidArg(data);

        var projectResult = await _mediator.Send(new GetProjectByIdQuery(projectId), ct);
        if (projectResult.IsFailed)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Проект не найден.", cancellationToken: ct);
            return;
        }

        var statuses = await GetStatusesAsync(ct);
        var (keyboard, _) = StatusSelectionKeyboard.Create(statuses, projectId, 1);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            $"Выберите новый статус для проекта «{projectResult.Value.Name}»:",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminSelectQuestionAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var questionId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "answer_question";
        session.PendingQuestionId = questionId;
        _stateService.Update(userId, session);

        var questionsResult = await _mediator.Send(new GetUnansweredQuestionsQuery(userId), ct);
        var questions = questionsResult.IsSuccess
            ? questionsResult.Value
            : new List<Application.Questions.DTOs.QuestionDto>();
        var selected = questions.FirstOrDefault(q => q.Id == questionId);

        var usernameLine = selected?.Username != null
            ? $"👤 @{selected.Username}\n\n"
            : "";

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"📝 <b>Вопрос от {selected?.FirstName ?? "пользователя"}</b>\n\n" +
            usernameLine +
            $"{selected?.Text}\n\n" +
            "Введите ваш ответ:",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

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

        var projectsResult = await _mediator.Send(new GetProjectsQuery(), ct);
        var projects = projectsResult.IsSuccess
            ? projectsResult.Value
            : new List<ProjectDto>();
        if (projects.Count == 0)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Нет проектов для рассылки.",
                cancellationToken: ct);
            return;
        }

        var result = await _mediator.Send(new BroadcastCommand(userId, projects.First().Id, text), ct);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result.IsSuccess
                ? "✅ Информационное сообщение отправлено всем подписанным пользователям."
                : result.Errors[0].Message,
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

    public async Task HandleAdminCreateProjectConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempProjectName == null) return;

        var addons = session.TempAddons?
            .Select(a => new NewAddonItem(a.Name, a.Description, a.Price))
            .ToList();

        var result = await _mediator.Send(new CreateProjectCommand(
            userId, session.TempProjectName, session.TempProjectDescription ?? "", addons), ct);

        var projectName = session.TempProjectName;
        _stateService.Remove(userId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            result.IsSuccess
                ? $"✅ Проект «{projectName}» создан."
                : result.Errors[0].Message,
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

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "✏️ <b>Редактирование дополнения</b>\n\n" +
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
        var projectResult = await _mediator.Send(new GetProjectIdByAddonQuery(addonId), ct);
        if (projectResult.IsFailed)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId, "Дополнение не найдено.", cancellationToken: ct);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да, удалить", CallbackData.Build("admin_del_addon_confirm", addonId)),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CallbackData.Build("admin_del_addon_cancel", projectResult.Value))
            }
        });

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "⚠️ <b>Удалить дополнение?</b>\n\nЭто действие нельзя отменить.",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleAdminDeleteAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var projectResult = await _mediator.Send(new GetProjectIdByAddonQuery(addonId), ct);
        if (projectResult.IsFailed)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message!.MessageId, "Дополнение не найдено.", cancellationToken: ct);
            return;
        }

        var result = await _mediator.Send(new DeleteAddonCommand(userId, addonId), ct);
        var resultText = result.IsSuccess ? "✅ Дополнение удалено." : result.Errors[0].Message;

        await ShowEditProjectFormAsync(botClient, query.Message!.Chat.Id, query.Message!.MessageId, projectResult.Value, userId, ct, resultText);
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

    public async Task HandleAdminStatusPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var (_, args) = CallbackData.Parse(data);
        var pageStr = args[1];
        if (pageStr == "nop") return;

        var projectId = Guid.Parse(args[0]);
        var page = int.Parse(pageStr);

        var projectResult = await _mediator.Send(new GetProjectByIdQuery(projectId), ct);
        if (projectResult.IsFailed) return;

        var statuses = await GetStatusesAsync(ct);
        var (keyboard, _) = StatusSelectionKeyboard.Create(statuses, projectId, page);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            $"Выберите новый статус для проекта «{projectResult.Value.Name}»:",
            replyMarkup: keyboard,
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
        var statuses = await GetStatusesAsync(ct);
        var order = statuses.Count > 0 ? statuses.Max(s => s.Order) + 1 : 1;

        var result = await _mediator.Send(
            new CreateProjectStatusCommand(userId, displayName, order, canEdit), ct);

        session.PendingAction = null;
        session.TempProjectId = null;
        session.TempStatusName = null;
        session.TempStatusCanEdit = null;
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            result.IsSuccess ? $"✅ Статус «{displayName}» добавлен." : result.Errors[0].Message,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    private async Task<List<ProjectStatusDto>> GetStatusesAsync(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectStatusesQuery(), ct);
        return result.IsSuccess ? result.Value : new List<ProjectStatusDto>();
    }
}
