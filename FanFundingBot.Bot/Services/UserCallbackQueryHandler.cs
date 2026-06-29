using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class UserCallbackQueryHandler : IUserCallbackQueryHandler
{
    private readonly IUserStateService _stateService;
    private readonly IOrderFlowService _orderFlowService;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUIRenderer _uiRenderer;
    private readonly IAdminService _adminService;
    private readonly IOrderProcessingQueue _processingQueue;
    private readonly MainMenuHelper _menuHelper;
    private readonly ProjectAccessHelper _projectAccess;

    public UserCallbackQueryHandler(
        IUserStateService stateService,
        IOrderFlowService orderFlowService,
        IProjectRepository projectRepository,
        IOrderRepository orderRepository,
        IUIRenderer uiRenderer,
        IAdminService adminService,
        IOrderProcessingQueue processingQueue,
        MainMenuHelper menuHelper,
        ProjectAccessHelper projectAccess)
    {
        _stateService = stateService;
        _orderFlowService = orderFlowService;
        _projectRepository = projectRepository;
        _orderRepository = orderRepository;
        _uiRenderer = uiRenderer;
        _adminService = adminService;
        _processingQueue = processingQueue;
        _menuHelper = menuHelper;
        _projectAccess = projectAccess;
    }

    public async Task HandleAddonToggleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null) return;

        if (!session.SelectedAddonIds.Remove(addonId))
        {
            session.SelectedAddonIds.Add(addonId);
            session.AddonQuantities[addonId] = 1;
        }
        else
        {
            session.AddonQuantities.Remove(addonId);
        }
        _stateService.Update(userId, session);

        var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId.Value);
        if (project == null) return;

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleAddonPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var pageStr = CallbackData.FirstArg(data);
        if (pageStr == "nop") return;

        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null) return;

        session.AddonPage = int.Parse(pageStr);
        _stateService.Update(userId, session);

        var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId.Value);
        if (project == null) return;

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null) return;

        if (session.SelectedAddonIds.Count == 0)
        {
            await botClient.AnswerCallbackQuery(
                query.Id,
                "Выберите хотя бы одно дополнение.",
                showAlert: true,
                cancellationToken: ct);
            return;
        }

        session.State = OrderFlowState.EnteringPhone;
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            "📞 Введите ваш номер телефона в формате +7XXXXXXXXXX или 8XXXXXXXXXX\n\n" +
            "Или отправьте контакт, используя кнопку 📎 -> Контакт.",
            replyMarkup: null,
            cancellationToken: ct);
    }

    public async Task HandleDeliveryAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var method = CallbackData.FirstArg(data);
        var session = _stateService.Get(userId);
        if (session == null) return;

        session.DeliveryMethod = method == "pickup" ? DeliveryMethod.Pickup : DeliveryMethod.Delivery;

        if (session.DeliveryMethod == DeliveryMethod.Pickup)
        {
            session.State = OrderFlowState.Confirming;
            _stateService.Update(userId, session);

            await _uiRenderer.ShowConfirmationAsync(
                botClient, query.Message!.Chat.Id, userId, ct);
        }
        else
        {
            session.State = OrderFlowState.EnteringAddress;
            _stateService.Update(userId, session);

            await botClient.EditMessageText(
                query.Message!.Chat.Id,
                query.Message!.MessageId,
                "🏠 Введите адрес доставки (город, улица, дом, квартира):",
                replyMarkup: null,
                cancellationToken: ct);
        }
    }

    public async Task HandleOrderConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null ||
            session.Phone == null || session.DeliveryMethod == null) return;

        var chatId = query.Message!.Chat.Id;
        var messageId = query.Message.MessageId;
        var isEditing = session.EditingOrderId.HasValue;
        var editingOrderId = session.EditingOrderId;

        var capturedProjectId = session.SelectedProjectId.Value;
        var capturedAddonIds = new HashSet<Guid>(session.SelectedAddonIds);
        var capturedQuantities = new Dictionary<Guid, int>(session.AddonQuantities);
        var capturedPhone = session.Phone;
        var capturedDelivery = session.DeliveryMethod.Value;
        var capturedAddress = session.Address;
        var capturedUserId = userId;

        _stateService.Remove(userId);

        await botClient.EditMessageText(
            chatId,
            messageId,
            isEditing
                ? "⏳ <b>Заказ обновляется...</b>\n\nВаш заказ в обработке. Мы сообщим вам о результате."
                : "⏳ <b>Заказ оформляется...</b>\n\nВаш заказ принят в обработку! Мы сообщим вам о результате.",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: null,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, chatId, userId, ct);

        var request = new OrderSaveRequest(
            UserId: capturedUserId,
            ChatId: chatId,
            MessageId: messageId,
            IsEditing: isEditing,
            EditingOrderId: editingOrderId,
            ProjectId: capturedProjectId,
            AddonIds: capturedAddonIds,
            AddonQuantities: capturedQuantities,
            Phone: capturedPhone,
            DeliveryMethod: capturedDelivery,
            Address: capturedAddress
        );

        await _processingQueue.EnqueueAsync(request, ct);
    }

    public async Task HandleOrderEditAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null) return;

        session.State = OrderFlowState.SelectingAddons;
        _stateService.Update(userId, session);

        var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId.Value);
        if (project == null) return;

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleOrderCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        _stateService.Remove(userId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "❌ Оформление заказа отменено.",
            replyMarkup: null,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleBackAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var step = CallbackData.FirstArg(data);
        var session = _stateService.Get(userId);
        if (session == null) return;

        switch (step)
        {
            case "phone":
                session.State = OrderFlowState.SelectingAddons;
                _stateService.Update(userId, session);

                if (session.SelectedProjectId != null)
                {
                    var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId.Value);
                    if (project != null)
                    {
                        await _uiRenderer.SendAddonSelectionAsync(
                            botClient, query.Message!.Chat.Id, userId, project, ct);
                    }
                }
                break;

            case "delivery":
                session.State = OrderFlowState.SelectingDelivery;
                _stateService.Update(userId, session);

                await botClient.EditMessageText(
                    query.Message!.Chat.Id,
                    query.Message!.MessageId,
                    "Выберите способ получения:",
                    replyMarkup: DeliveryMethodKeyboard.Create(),
                    cancellationToken: ct);
                break;

            default:
                break;
        }
    }

    // --- Addon quantity inc/dec on confirmation screen ---

    public async Task HandleAddonQtyIncAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.Get(userId);
        if (session == null) return;

        if (session.AddonQuantities.ContainsKey(addonId))
        {
            session.AddonQuantities[addonId]++;
            _stateService.Update(userId, session);
        }

        await _uiRenderer.ShowConfirmationAsync(
            botClient, query.Message!.Chat.Id, userId, ct);
    }

    public async Task HandleAddonQtyDecAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var addonId = CallbackData.GuidArg(data);
        var session = _stateService.Get(userId);
        if (session == null) return;

        if (session.AddonQuantities.TryGetValue(addonId, out var qty) && qty > 1)
        {
            session.AddonQuantities[addonId] = qty - 1;
            _stateService.Update(userId, session);
        }

        await _uiRenderer.ShowConfirmationAsync(
            botClient, query.Message!.Chat.Id, userId, ct);
    }

    // --- Edit existing order from My Order ---

    public async Task HandleOrderEditMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var orderId = CallbackData.GuidArg(data);
        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order == null)
        {
            await botClient.AnswerCallbackQuery(query.Id, "Заказ не найден.", showAlert: true, cancellationToken: ct);
            return;
        }

        var (allowed, error) = await _projectAccess.CanEditOrdersAsync(order.ProjectId);
        if (!allowed)
        {
            await botClient.AnswerCallbackQuery(query.Id, error, showAlert: true, cancellationToken: ct);
            return;
        }

        var project = await _projectRepository.GetByIdAsync(order.ProjectId);
        var session = _stateService.GetOrCreate(userId);
        session.EditingOrderId = order.Id;
        session.SelectedProjectId = order.ProjectId;
        session.SelectedAddonIds = order.OrderAddons.Select(oa => oa.AddonId).ToHashSet();
        session.AddonQuantities = order.OrderAddons.ToDictionary(oa => oa.AddonId, oa => oa.Quantity);
        session.Phone = order.Phone;
        session.DeliveryMethod = order.DeliveryMethod;
        session.Address = order.Address;
        session.State = OrderFlowState.SelectingAddons;
        session.AddonPage = 1;
        session.EditMessageId = null;
        _stateService.Update(userId, session);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            "✏️ Редактирование заказа.",
            replyMarkup: null,
            cancellationToken: ct);

        await botClient.SendMessage(
            query.Message.Chat.Id,
            "Выберите дополнения:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message.Chat.Id, userId, project, ct);
    }

    // --- Delete order from My Order ---

    public async Task HandleOrderDeleteMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var orderId = CallbackData.GuidArg(data);

        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order == null)
        {
            await botClient.AnswerCallbackQuery(query.Id, "Заказ не найден.", showAlert: true, cancellationToken: ct);
            return;
        }

        var (allowed, error) = await _projectAccess.CanEditOrdersAsync(order.ProjectId);
        if (!allowed)
        {
            await botClient.AnswerCallbackQuery(query.Id, error, showAlert: true, cancellationToken: ct);
            return;
        }

        var keyboard = DeleteConfirmationKeyboard.Create(orderId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            "⚠️ <b>Вы уверены, что хотите удалить заказ?</b>\n\nЭто действие нельзя отменить.",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleOrderDeleteMyConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var orderId = CallbackData.GuidArg(data);

        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order == null)
        {
            await botClient.AnswerCallbackQuery(query.Id, "Заказ не найден.", showAlert: true, cancellationToken: ct);
            return;
        }

        var (allowed, error) = await _projectAccess.CanEditOrdersAsync(order.ProjectId);
        if (!allowed)
        {
            await botClient.AnswerCallbackQuery(query.Id, error, showAlert: true, cancellationToken: ct);
            return;
        }

        await _orderFlowService.DeleteOrderAsync(orderId);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "✅ Заказ удалён.",
            replyMarkup: null,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleOrderDeleteMyCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var orderId = CallbackData.GuidArg(data);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "❌ Удаление отменено.",
            replyMarkup: null,
            cancellationToken: ct);

        // Re-send order info
        var order = await _orderRepository.GetByIdWithAddonsAsync(orderId);
        if (order != null)
        {
            await _uiRenderer.HandleMyOrderByOrderAsync(botClient, query.Message.Chat.Id, order, ct);
        }
    }
}
