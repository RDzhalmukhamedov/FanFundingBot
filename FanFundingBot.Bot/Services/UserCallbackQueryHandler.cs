using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class UserCallbackQueryHandler : IUserCallbackQueryHandler
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;
    private readonly IUIRenderer _uiRenderer;
    private readonly MainMenuHelper _menuHelper;
    private readonly ILogger<UserCallbackQueryHandler> _logger;

    public UserCallbackQueryHandler(
        IUserStateService stateService,
        IMediator mediator,
        IUIRenderer uiRenderer,
        MainMenuHelper menuHelper,
        ILogger<UserCallbackQueryHandler>? logger = null)
    {
        _stateService = stateService;
        _mediator = mediator;
        _uiRenderer = uiRenderer;
        _menuHelper = menuHelper;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<UserCallbackQueryHandler>.Instance;
    }

    public async Task HandleAddonToggleAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var addonId))
        {
            _logger.LogWarning("Invalid callback data for addon toggle: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null)
        {
            _logger.LogDebug("HandleAddonToggle: no session/project for user {UserId}", userId);
            return;
        }

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

        var project = await GetProjectAsync(session.SelectedProjectId.Value, ct);
        if (project == null) return;

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleAddonPageAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var pageStr = CallbackData.FirstArg(data);
        if (pageStr == "nop") return;

        if (!int.TryParse(pageStr, out var page))
        {
            _logger.LogWarning("Invalid page callback data: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null)
        {
            _logger.LogDebug("HandleAddonPage: no session/project for user {UserId}", userId);
            return;
        }

        session.AddonPage = page;
        _stateService.Update(userId, session);

        var project = await GetProjectAsync(session.SelectedProjectId.Value, ct);
        if (project == null) return;

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleAddonConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null)
        {
            _logger.LogDebug("HandleAddonConfirm: no session for user {UserId}", userId);
            return;
        }

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
            "📞 Введите ваш номер телефона в формате +XXXXXXXXXXX (8–15 цифр)\n\n" +
            "Или отправьте контакт, используя кнопку 📎 -> Контакт.",
            replyMarkup: null,
            cancellationToken: ct);
    }

    public async Task HandleDeliveryAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var method = CallbackData.FirstArg(data);
        var session = _stateService.Get(userId);
        if (session == null)
        {
            _logger.LogDebug("HandleDelivery: no session for user {UserId}", userId);
            return;
        }

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
            session.Phone == null || session.DeliveryMethod == null)
        {
            _logger.LogWarning("HandleOrderConfirm: incomplete session for user {UserId}", userId);
            await botClient.AnswerCallbackQuery(query.Id, "Данные заказа неполны. Начните заново.", showAlert: true, cancellationToken: ct);
            return;
        }

        var chatId = query.Message!.Chat.Id;
        var isEditing = session.EditingOrderId.HasValue;

        if (isEditing)
        {
            var updateResult = await _mediator.Send(new UpdateOrderCommand(
                session.EditingOrderId!.Value, userId,
                new Dictionary<Guid, int>(session.AddonQuantities),
                session.Phone, session.DeliveryMethod.Value, session.Address), ct);

            if (updateResult.IsFailed)
            {
                _logger.LogWarning("UpdateOrder failed for user {UserId} order {OrderId}: {Errors}",
                    userId, session.EditingOrderId, string.Join("; ", updateResult.Errors.Select(e => e.Message)));
                await botClient.SendMessage(
                    chatId,
                    $"❌ Не удалось обновить заказ: {updateResult.Errors[0].Message}\n\nПопробуйте позже.",
                    cancellationToken: ct);
                return;
            }

            _stateService.Remove(userId);

            await botClient.SendMessage(
                chatId,
                "✅ <b>Заказ обновлён!</b>\n\nВаш заказ успешно изменён.",
                parseMode: ParseMode.Html,
                cancellationToken: ct);

            await _menuHelper.SendReturnToMenuAsync(botClient, chatId, userId, ct);
            return;
        }

        var createResult = await _mediator.Send(new CreateOrderCommand(
            userId, session.SelectedProjectId.Value,
            new Dictionary<Guid, int>(session.AddonQuantities),
            session.Phone, session.DeliveryMethod.Value, session.Address), ct);

        if (createResult.IsFailed)
        {
            _logger.LogWarning("CreateOrder failed for user {UserId} project {ProjectId}: {Errors}",
                userId, session.SelectedProjectId, string.Join("; ", createResult.Errors.Select(e => e.Message)));
            await botClient.SendMessage(
                chatId,
                $"❌ Не удалось оформить заказ: {createResult.Errors[0].Message}\n\nПопробуйте позже.",
                cancellationToken: ct);
            return;
        }

        _stateService.Remove(userId);

        await botClient.SendMessage(
            chatId,
            $"✅ <b>Заказ успешно оформлен!</b>\n\nНомер заказа: <code>{createResult.Value:N}</code>\n\nМы свяжемся с вами в ближайшее время.",
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, chatId, userId, ct);
    }

    public async Task HandleOrderEditAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null || session.SelectedProjectId == null)
        {
            _logger.LogDebug("HandleOrderEdit: no session/project for user {UserId}", userId);
            return;
        }

        session.State = OrderFlowState.SelectingAddons;
        _stateService.Update(userId, session);

        var project = await GetProjectAsync(session.SelectedProjectId.Value, ct);
        if (project == null)
        {
            _logger.LogDebug("HandleOrderEdit: project not found for user {UserId}", userId);
            await botClient.AnswerCallbackQuery(query.Id, "Проект не найден.", showAlert: true, cancellationToken: ct);
            return;
        }

        await _uiRenderer.SendAddonSelectionAsync(
            botClient, query.Message!.Chat.Id, userId, project, ct);
    }

    public async Task HandleOrderCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        _logger.LogDebug("Order cancel for user {UserId}", userId);
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
        if (session == null)
        {
            _logger.LogDebug("HandleBack: no session for user {UserId}", userId);
            return;
        }

        switch (step)
        {
            case "phone":
                session.State = OrderFlowState.SelectingAddons;
                _stateService.Update(userId, session);

                if (session.SelectedProjectId != null)
                {
                    var project = await GetProjectAsync(session.SelectedProjectId.Value, ct);
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

    public async Task HandleAddonQtyIncAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var addonId))
        {
            _logger.LogWarning("Invalid qty inc data: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null)
        {
            _logger.LogDebug("HandleQtyInc: no session for user {UserId}", userId);
            return;
        }

        if (!session.AddonQuantities.ContainsKey(addonId))
        {
            await botClient.AnswerCallbackQuery(query.Id, "Дополнение не выбрано.", showAlert: true, cancellationToken: ct);
            return;
        }

        session.AddonQuantities[addonId]++;
        _stateService.Update(userId, session);

        await _uiRenderer.ShowConfirmationAsync(
            botClient, query.Message!.Chat.Id, userId, ct);
    }

    public async Task HandleAddonQtyDecAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var addonId))
        {
            _logger.LogWarning("Invalid qty dec data: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null)
        {
            _logger.LogDebug("HandleQtyDec: no session for user {UserId}", userId);
            return;
        }

        if (!session.AddonQuantities.TryGetValue(addonId, out var qty) || qty <= 1)
        {
            await botClient.AnswerCallbackQuery(
                query.Id,
                "Количество не может быть меньше 1. Для удаления уберите дополнение.",
                showAlert: true,
                cancellationToken: ct);
            return;
        }

        session.AddonQuantities[addonId] = qty - 1;
        _stateService.Update(userId, session);

        await _uiRenderer.ShowConfirmationAsync(
            botClient, query.Message!.Chat.Id, userId, ct);
    }

    public async Task HandleOrderEditMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var orderId))
        {
            _logger.LogWarning("Invalid orderId in HandleOrderEditMy: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }
        var orderResult = await _mediator.Send(new GetOrderByIdQuery(orderId, userId), ct);
        if (orderResult.IsFailed)
        {
            await botClient.AnswerCallbackQuery(query.Id, orderResult.Errors[0].Message, showAlert: true, cancellationToken: ct);
            return;
        }

        var order = orderResult.Value;
        var project = await GetProjectAsync(order.ProjectId, ct);
        if (project == null || !project.CanEditOrders)
        {
            await botClient.AnswerCallbackQuery(
                query.Id,
                project == null ? "Проект не найден." : "Редактирование заказа недоступно для текущего статуса проекта.",
                showAlert: true, cancellationToken: ct);
            return;
        }

        var session = _stateService.GetOrCreate(userId);
        session.EditingOrderId = order.Id;
        session.SelectedProjectId = order.ProjectId;
        session.SelectedAddonIds = order.Positions.Select(p => p.AddonId).ToHashSet();
        session.AddonQuantities = order.Positions.ToDictionary(p => p.AddonId, p => p.Quantity);
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

    public async Task HandleOrderDeleteMyAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var orderId))
        {
            _logger.LogWarning("Invalid orderId in HandleOrderDeleteMy: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var orderResult = await _mediator.Send(new GetOrderByIdQuery(orderId, userId), ct);
        if (orderResult.IsFailed)
        {
            await botClient.AnswerCallbackQuery(query.Id, orderResult.Errors[0].Message, showAlert: true, cancellationToken: ct);
            return;
        }

        if (!await CanEditOrderAsync(botClient, query, orderResult.Value.ProjectId, ct))
            return;

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
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var orderId))
        {
            _logger.LogWarning("Invalid orderId in HandleOrderDeleteMyConfirm: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        var orderResult = await _mediator.Send(new GetOrderByIdQuery(orderId, userId), ct);
        if (orderResult.IsFailed)
        {
            await botClient.AnswerCallbackQuery(query.Id, orderResult.Errors[0].Message, showAlert: true, cancellationToken: ct);
            return;
        }

        if (!await CanEditOrderAsync(botClient, query, orderResult.Value.ProjectId, ct))
            return;

        var result = await _mediator.Send(new DeleteOrderCommand(orderId, userId), ct);
        if (result.IsFailed)
        {
            await botClient.AnswerCallbackQuery(query.Id, result.Errors[0].Message, showAlert: true, cancellationToken: ct);
            return;
        }

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
        if (!Guid.TryParse(CallbackData.FirstArg(data), out var orderId))
        {
            _logger.LogWarning("Invalid orderId in HandleOrderDeleteMyCancel: {Data}", data);
            await botClient.AnswerCallbackQuery(query.Id, "Некорректные данные.", cancellationToken: ct);
            return;
        }

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "❌ Удаление отменено.",
            replyMarkup: null,
            cancellationToken: ct);

        var orderResult = await _mediator.Send(new GetOrderByIdQuery(orderId, userId), ct);
        if (orderResult.IsSuccess)
        {
            await _uiRenderer.HandleMyOrderByOrderAsync(botClient, query.Message.Chat.Id, orderResult.Value, ct);
        }
    }

    private async Task<ProjectDto?> GetProjectAsync(Guid projectId, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProjectByIdQuery(projectId), ct);
        return result.IsSuccess ? result.Value : null;
    }

    private async Task<bool> CanEditOrderAsync(
        ITelegramBotClient botClient, CallbackQuery query, Guid projectId, CancellationToken ct)
    {
        var project = await GetProjectAsync(projectId, ct);
        if (project == null || !project.CanEditOrders)
        {
            await botClient.AnswerCallbackQuery(
                query.Id,
                project == null ? "Проект не найден." : "Редактирование заказа недоступно для текущего статуса проекта.",
                showAlert: true, cancellationToken: ct);
            return false;
        }

        return true;
    }
}
