using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Enums;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class UIRenderer : IUIRenderer
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;

    public UIRenderer(
        IUserStateService stateService,
        IMediator mediator)
    {
        _stateService = stateService;
        _mediator = mediator;
    }

    public async Task ShowConfirmationAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null) return;

        var projectResult = await _mediator.Send(
            new GetProjectByIdQuery(session.SelectedProjectId!.Value), ct);
        if (projectResult.IsFailed)
            return;

        var project = projectResult.Value;
        var addonLines = new List<string>();
        var addonKeyboardItems = new List<(Guid Id, string Name, decimal Price, int Quantity)>();

        foreach (var addonId in session.SelectedAddonIds)
        {
            var addon = project.Addons.FirstOrDefault(a => a.Id == addonId);
            if (addon != null)
            {
                var qty = session.AddonQuantities.TryGetValue(addonId, out var q) ? q : 1;
                var total = addon.Price * qty;
                addonLines.Add($"{addon.Name} x{qty} — {total:F2}₽");
                addonKeyboardItems.Add((addon.Id, addon.Name, addon.Price, qty));
            }
        }

        var deliveryName = session.DeliveryMethod == DeliveryMethod.Pickup ? "🚶 Самовывоз" : "🚚 Доставка";
        var totalPrice = addonKeyboardItems.Sum(a => a.Price * a.Quantity);

        var text = $"📋 <b>Подтверждение заказа</b>\n\n" +
                   $"<b>Проект:</b> {project.Name}\n" +
                   $"<b>Дополнения:</b>\n{string.Join("\n", addonLines.Select(a => $"  • {a}"))}\n" +
                   $"<b>Итого:</b> {totalPrice:F2}₽\n\n" +
                   $"<b>Телефон:</b> {session.Phone}\n" +
                   $"<b>Способ получения:</b> {deliveryName}\n" +
                   (session.Address != null ? $"<b>Адрес:</b> {session.Address}\n" : "") +
                   "\nВсё верно?";

        var keyboard = addonKeyboardItems.Count > 0
            ? ConfirmationKeyboard.Create(addonKeyboardItems, session.EditingOrderId == null)
            : ConfirmationKeyboard.Create(session.EditingOrderId == null);

        if (session.EditMessageId.HasValue)
        {
            await botClient.EditMessageText(
                chatId, session.EditMessageId.Value, text,
                parseMode: ParseMode.Html,
                replyMarkup: keyboard,
                cancellationToken: ct);
            _stateService.Update(userId, session);

            return;
        }

        var sent = await botClient.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
        session.EditMessageId = sent.MessageId;
        _stateService.Update(userId, session);
    }

    public async Task SendAddonSelectionAsync(
        ITelegramBotClient botClient,
        long chatId,
        long userId,
        ProjectDto project,
        CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null) return;

        var pageItems = project.Addons
            .Skip((session.AddonPage - 1) * AddonSelectionKeyboard.PageSize)
            .Take(AddonSelectionKeyboard.PageSize)
            .ToList();

        var (keyboard, _) = AddonSelectionKeyboard.Create(
            pageItems, session.SelectedAddonIds, session.AddonPage, project.Addons.Count);

        var text = $"🛒 <b>Оформление заказа: {project.Name}</b>\n\n" +
                   "Выберите нужные дополнения. При повторном нажатии выбор снимается.\n\n" +
                   $"Выбрано: {session.SelectedAddonIds.Count} дополнений";

        if (session.EditMessageId.HasValue)
        {
            try
            {
                await botClient.EditMessageText(
                    chatId,
                    session.EditMessageId.Value,
                    text,
                    parseMode: ParseMode.Html,
                    replyMarkup: keyboard,
                    cancellationToken: ct);
                return;
            }
            catch
            {
            }
        }

        var sent = await botClient.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
        session.EditMessageId = sent.MessageId;
        _stateService.Update(userId, session);
    }

    public async Task HandleMyOrderByOrderAsync(ITelegramBotClient botClient, long chatId, OrderDto order, CancellationToken ct)
    {
        var statusName = order.Status switch
        {
            OrderStatus.New => "🆕 Новый",
            OrderStatus.Confirmed => "✅ Подтверждён",
            OrderStatus.Completed => "✔️ Выполнен",
            OrderStatus.Cancelled => "❌ Отменён",
            _ => "❓ Неизвестно"
        };
        var deliveryName = order.DeliveryMethod switch
        {
            DeliveryMethod.Pickup => "🚶 Самовывоз",
            DeliveryMethod.Delivery => "🚚 Доставка",
            _ => "❓"
        };

        var addonLines = order.Positions
            .Select(p => $"  • {p.AddonName} x{p.Quantity} — {p.Price * p.Quantity:F2}₽")
            .ToList();

        var text = $"📦 <b>Ваш заказ</b>\n\n" +
                   $"Проект: {order.ProjectName}\n" +
                   $"Статус: {statusName}\n" +
                   $"Телефон: {order.Phone}\n" +
                   $"Способ получения: {deliveryName}\n" +
                   (order.Address != null ? $"Адрес: {order.Address}\n" : "") +
                   (addonLines.Count > 0 ? $"\n<b>Дополнения:</b>\n{string.Join("\n", addonLines)}\n" : "") +
                   $"\n<b>Итого:</b> {order.Total:F2}₽\n" +
                   $"Дата: {order.CreatedAt:dd.MM.yyyy HH:mm}";

        var canEdit = false;
        var projectResult = await _mediator.Send(new GetProjectByIdQuery(order.ProjectId), ct);
        if (projectResult.IsSuccess)
            canEdit = projectResult.Value.CanEditOrders;

        var buttons = new List<InlineKeyboardButton[]>();

        if (canEdit)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", CallbackData.Build("order_edit_my", order.Id))
            });
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Удалить заказ", CallbackData.Build("order_delete_my", order.Id))
            });
        }

        var keyboard = new InlineKeyboardMarkup(buttons);

        await botClient.SendMessage(
            chatId,
            text,
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}
