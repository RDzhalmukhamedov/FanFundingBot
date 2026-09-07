using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Users.Commands;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Enums;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Handlers;

public class CommandHandler : ICommandHandler
{
    private readonly IUserStateService _stateService;
    private readonly IAuthorizationService _authorization;
    private readonly IMediator _mediator;
    private readonly TelegramUserContext _userContext;
    private readonly BotConfiguration _config;
    private readonly IUIRenderer _uiRenderer;
    private readonly ITextInputHandler _textInputHandler;
    private readonly MainMenuHelper _menuHelper;
    private readonly IAdminCommandHandler _adminCommandHandler;

    public CommandHandler(
        IUserStateService stateService,
        IAuthorizationService authorization,
        IMediator mediator,
        TelegramUserContext userContext,
        BotConfiguration config,
        IUIRenderer uiRenderer,
        ITextInputHandler textInputHandler,
        MainMenuHelper menuHelper,
        IAdminCommandHandler adminCommandHandler)
    {
        _stateService = stateService;
        _authorization = authorization;
        _mediator = mediator;
        _userContext = userContext;
        _config = config;
        _uiRenderer = uiRenderer;
        _textInputHandler = textInputHandler;
        _menuHelper = menuHelper;
        _adminCommandHandler = adminCommandHandler;
    }

    public async Task HandleCommandAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;
        _userContext.UserId = userId;

        await EnsureUserExistsAsync(message);

        await (text switch
        {
            "/start" => HandleStartAsync(botClient, message, ct),
            "📋 Статус проекта" => HandleProjectStatusAsync(botClient, message, ct),
            "📦 Мой заказ" => HandleMyOrderAsync(botClient, message, ct),
            "🛒 Оформить заказ" => HandleStartOrderAsync(botClient, message, ct),
            "❓ Задать вопрос" => HandleAskQuestionAsync(botClient, message, ct),
            "ℹ️ О проекте" => HandleProjectInfoAsync(botClient, message, ct),
            "🔄 Обновить статус" => _adminCommandHandler.HandleAdminUpdateStatusAsync(botClient, message, ct),
            "📢 Рассылка" => _adminCommandHandler.HandleAdminBroadcastAsync(botClient, message, ct),
            "✉️ Ответить на вопрос" => _adminCommandHandler.HandleAdminAnswerQuestionAsync(botClient, message, ct),
            "➕ Создать проект" => _adminCommandHandler.HandleAdminCreateProjectAsync(botClient, message, ct),
            "✏️ Редактировать проект" => _adminCommandHandler.HandleAdminEditProjectListAsync(botClient, message, ct),
            "◀️ Назад" => HandleBackAsync(botClient, message, ct),
            _ => _textInputHandler.HandleTextInputAsync(botClient, message, ct)
        });
    }

    private async Task HandleStartAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var isAdmin = await _authorization.CanCreateProjectAsync(userId);

        var welcomeText = "👋 Добро пожаловать в FanFundingBot!\n\n" +
                          "Здесь вы можете оформить заказ, узнать статус проекта и задать вопросы.\n\n" +
                          "Используйте кнопки меню ниже 👇";

        await botClient.SendMessage(
            chatId: message.Chat.Id,
            text: welcomeText,
            replyMarkup: MainMenuKeyboard.Create(isAdmin),
            cancellationToken: ct);
    }

    private async Task HandleProjectStatusAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projectsResult = await _mediator.Send(new GetProjectsQuery(), ct);
        var projects = projectsResult.IsSuccess ? projectsResult.Value : new List<Application.Projects.DTOs.ProjectDto>();
        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В данный момент нет активных проектов.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
                cancellationToken: ct);
            return;
        }

        var project = projects.First();

        var text = $"📋 <b>Проект: {project.Name}</b>\n\n" +
                   $"Статус: {project.StatusName}\n" +
                   (project.Description != null ? $"Описание: <i>{project.Description}</i>" : "");

        await botClient.SendMessage(
            message.Chat.Id,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
            cancellationToken: ct);
    }

    private async Task HandleMyOrderAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var ordersResult = await _mediator.Send(new GetMyOrdersQuery(userId), ct);
        var order = ordersResult.IsSuccess ? ordersResult.Value.FirstOrDefault() : null;

        if (order == null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "У вас нет заказов.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
                cancellationToken: ct);
            return;
        }

        await _uiRenderer.HandleMyOrderByOrderAsync(botClient, message.Chat.Id, order, ct);
    }

    private async Task HandleStartOrderAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projectsResult = await _mediator.Send(new GetProjectsQuery(), ct);
        var projects = projectsResult.IsSuccess ? projectsResult.Value : new List<Application.Projects.DTOs.ProjectDto>();

        var activeProject = projects.FirstOrDefault(p => p.CanEditOrders);
        if (activeProject == null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В данный момент нет доступных для заказа проектов.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
                cancellationToken: ct);
            return;
        }

        if (activeProject.Addons.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В выбранном проекте нет доступных дополнений.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
                cancellationToken: ct);
            return;
        }

        var ordersResult = await _mediator.Send(new GetMyOrdersQuery(userId), ct);
        var existingOrder = ordersResult.IsSuccess ? ordersResult.Value.FirstOrDefault() : null;
        if (existingOrder is { Status: OrderStatus.New or OrderStatus.Confirmed })
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "У вас уже есть оформленный заказ. Вы можете оформить новый только после завершения текущего.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
                cancellationToken: ct);
            return;
        }

        var session = _stateService.GetOrCreate(userId);
        session.State = OrderFlowState.SelectingAddons;
        session.SelectedProjectId = activeProject.Id;
        session.SelectedAddonIds.Clear();
        session.AddonPage = 1;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "🛒 Оформление заказа. Выберите дополнения:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);

        await _uiRenderer.SendAddonSelectionAsync(botClient, message.Chat.Id, userId, activeProject, ct);
    }

    private async Task HandleAskQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.GetOrCreate(userId);
        session.State = OrderFlowState.Idle;
        session.PendingAction = "ask_question";
        session.EditMessageId = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "📝 Пожалуйста, напишите ваш вопрос. Мы ответим вам в ближайшее время.",
            replyMarkup: BackKeyboard.Create(),
            cancellationToken: ct);
    }

    private async Task HandleProjectInfoAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var projectsResult = await _mediator.Send(new GetProjectsQuery(), ct);
        var projects = projectsResult.IsSuccess ? projectsResult.Value : new List<Application.Projects.DTOs.ProjectDto>();
        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Информация о проектах отсутствует.",
                replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(message.From!.Id)),
                cancellationToken: ct);
            return;
        }

        var project = projects.First();
        var text = $"ℹ️ <b>{project.Name}</b>\n\n" +
                   (project.Description != null ? $"<i>{project.Description}</i>\n\n" : "") +
                   "<b>Дополнения:</b>\n";

        if (project.Addons.Count == 0)
        {
            text += "Нет дополнений.";
        }
        else
        {
            foreach (var addon in project.Addons)
            {
                text += $"\n• <b>{addon.Name}</b> — {addon.Price:F2}₽";
                if (addon.Description != null)
                    text += $"\n  <i>{addon.Description}</i>";
            }
        }

        await botClient.SendMessage(
            message.Chat.Id,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(message.From!.Id)),
            cancellationToken: ct);
    }

    private async Task HandleBackAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.Get(userId);

        if (session != null)
        {
            switch (session.State)
            {
                case OrderFlowState.EnteringPhone:
                    session.State = OrderFlowState.SelectingAddons;
                    session.Phone = null;
                    _stateService.Update(userId, session);

                    var projectResult = await _mediator.Send(
                        new Application.Projects.Queries.GetProjectByIdQuery(session.SelectedProjectId!.Value), ct);
                    if (projectResult.IsSuccess)
                        await _uiRenderer.SendAddonSelectionAsync(botClient, message.Chat.Id, userId, projectResult.Value, ct);
                    return;

                case OrderFlowState.EnteringAddress:
                    session.State = OrderFlowState.SelectingDelivery;
                    session.Address = null;
                    session.DeliveryMethod = null;
                    _stateService.Update(userId, session);

                    var sent = await botClient.SendMessage(
                        message.Chat.Id,
                        "Выберите способ получения:",
                        replyMarkup: DeliveryMethodKeyboard.Create(),
                        cancellationToken: ct);
                    session.EditMessageId = sent.MessageId;
                    _stateService.Update(userId, session);
                    return;
            }
        }

        _stateService.Remove(userId);
        await _menuHelper.SendReturnToMenuAsync(botClient, message.Chat.Id, userId, ct);
    }

    public async Task HandlePhoneContactAsync(ITelegramBotClient botClient, Message message, string phone, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.Get(userId);

        if (session?.State != OrderFlowState.EnteringPhone)
        {
            return;
        }

        var error = InputValidation.ValidatePhone(phone);
        if (error != null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                error,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        session.Phone = phone;
        session.State = OrderFlowState.SelectingDelivery;
        _stateService.Update(userId, session);

        var sent = await botClient.SendMessage(
            message.Chat.Id,
            "Выберите способ получения:",
            replyMarkup: DeliveryMethodKeyboard.Create(),
            cancellationToken: ct);
        session.EditMessageId = sent.MessageId;
        _stateService.Update(userId, session);
    }

    private async Task EnsureUserExistsAsync(Message message)
    {
        var from = message.From!;
        await _mediator.Send(new GetOrCreateUserCommand(
            from.Id, from.FirstName, from.LastName, from.Username,
            from.Id == _config.OwnerId));
    }
}
