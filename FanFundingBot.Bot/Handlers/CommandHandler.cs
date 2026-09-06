using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Handlers;

public class CommandHandler : ICommandHandler
{
    private readonly IUserRepository _userRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IUserStateService _stateService;
    private readonly IAdminService _adminService;
    private readonly IOrderFlowService _orderFlowService;
    private readonly BotConfiguration _config;
    private readonly IUIRenderer _uiRenderer;
    private readonly ITextInputHandler _textInputHandler;
    private readonly MainMenuHelper _menuHelper;
    private readonly IAdminCommandHandler _adminCommandHandler;

    public CommandHandler(
        IUserRepository userRepository,
        IProjectRepository projectRepository,
        IUserStateService stateService,
        IAdminService adminService,
        IOrderFlowService orderFlowService,
        BotConfiguration config,
        IUIRenderer uiRenderer,
        ITextInputHandler textInputHandler,
        MainMenuHelper menuHelper,
        IAdminCommandHandler adminCommandHandler)
    {
        _userRepository = userRepository;
        _projectRepository = projectRepository;
        _stateService = stateService;
        _adminService = adminService;
        _orderFlowService = orderFlowService;
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
        var isAdmin = await _adminService.IsAdminAsync(userId);

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
        var projects = await _projectRepository.GetAllAsync();
        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В данный момент нет активных проектов.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(message.From!.Id)),
                cancellationToken: ct);
            return;
        }

        var project = projects.First();

        var text = $"📋 <b>Проект: {project.Name}</b>\n\n" +
                   $"Статус: {project.Status?.DisplayName ?? "❓ Неизвестно"}\n" +
                   (project.Description != null ? $"Описание: <i>{project.Description}</i>" : "");

        await botClient.SendMessage(
            message.Chat.Id,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(message.From!.Id)),
            cancellationToken: ct);
    }

    private async Task HandleMyOrderAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var order = await _orderFlowService.GetLatestOrderWithAddonsAsync(userId);

        if (order == null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "У вас нет заказов.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(message.From!.Id)),
                cancellationToken: ct);
            return;
        }

        await _uiRenderer.HandleMyOrderByOrderAsync(botClient, message.Chat.Id, order, ct);
    }

    private async Task HandleStartOrderAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projects = await _projectRepository.GetAllWithAddonsAsync();

        var activeProject = projects.FirstOrDefault(p => p.Status?.CanEditOrders == true);
        if (activeProject == null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В данный момент нет доступных для заказа проектов.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(userId)),
                cancellationToken: ct);
            return;
        }

        if (activeProject.Addons.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "В выбранном проекте нет доступных дополнений.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(userId)),
                cancellationToken: ct);
            return;
        }

        var existingOrder = await _orderFlowService.GetLatestOrderAsync(userId);
        if (existingOrder is { Status: OrderStatus.New or OrderStatus.Confirmed })
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "У вас уже есть оформленный заказ. Вы можете оформить новый только после завершения текущего.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(userId)),
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
        var projects = await _projectRepository.GetAllWithAddonsAsync();
        if (projects.Count == 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Информация о проектах отсутствует.",
                replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(message.From!.Id)),
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
            replyMarkup: MainMenuKeyboard.Create(await _adminService.IsAdminAsync(message.From!.Id)),
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

                    var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId!.Value);
                    if (project != null)
                        await _uiRenderer.SendAddonSelectionAsync(botClient, message.Chat.Id, userId, project, ct);
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

        var error = _orderFlowService.ValidatePhone(phone);
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
        var userId = message.From!.Id;
        if (await _userRepository.ExistsAsync(userId))
        {
            await _adminService.EnsureAdminRoleAsync(userId);
            return;
        }

        var user = new Core.Entities.Customer
        {
            Id = userId,
            FirstName = message.From.FirstName,
            LastName = message.From.LastName,
            Username = message.From.Username,
            Role = userId == _config.OwnerId ? UserRole.Admin : UserRole.Regular,
            CreatedAt = DateTime.UtcNow
        };

        await _userRepository.AddAsync(user);
    }
}
