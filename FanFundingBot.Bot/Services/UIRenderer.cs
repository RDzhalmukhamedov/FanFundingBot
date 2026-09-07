using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class UIRenderer : IUIRenderer
{
    private readonly IUserStateService _stateService;
    private readonly IProjectRepository _projectRepository;
    private readonly IOrderFlowService _orderFlowService;

    public UIRenderer(
        IUserStateService stateService,
        IProjectRepository projectRepository,
        IOrderFlowService orderFlowService)
    {
        _stateService = stateService;
        _projectRepository = projectRepository;
        _orderFlowService = orderFlowService;
    }

    public async Task ShowConfirmationAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null) return;

        var project = await _projectRepository.GetByIdWithAddonsAsync(session.SelectedProjectId!.Value);
        var addonLines = new List<string>();
        var addonKeyboardItems = new List<(Guid Id, string Name, decimal Price, int Quantity)>();

        foreach (var addonId in session.SelectedAddonIds)
        {
            var addon = project?.Addons.FirstOrDefault(a => a.Id == addonId);
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
                   $"<b>Проект:</b> {project?.Name}\n" +
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
        Project project,
        CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session == null) return;

        var addons = await _orderFlowService.GetAddonsPagedAsync(
            project.Id, session.AddonPage, 6);
        var totalCount = await _orderFlowService.GetAddonCountAsync(project.Id);

        var (keyboard, _) = AddonSelectionKeyboard.Create(
            addons, session.SelectedAddonIds, session.AddonPage, totalCount);

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

    public async Task HandleMyOrderByOrderAsync(ITelegramBotClient botClient, long chatId, Order order, CancellationToken ct)
    {
        var userId = order.User.Id;
        var project = await _projectRepository.GetByIdAsync(order.Project.Id);
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

        var addonLines = order.OrderAddons
            .Select(oa => $"  • {oa.Addon.Name} x{oa.Quantity} — {oa.Addon.Price * oa.Quantity:F2}₽")
            .ToList();
        var totalPrice = order.OrderAddons.Sum(oa => oa.Addon.Price * oa.Quantity);

        var text = $"📦 <b>Ваш заказ</b>\n\n" +
                   $"Проект: {project?.Name ?? "Неизвестно"}\n" +
                   $"Статус: {statusName}\n" +
                   $"Телефон: {order.Phone}\n" +
                   $"Способ получения: {deliveryName}\n" +
                   (order.Address != null ? $"Адрес: {order.Address}\n" : "") +
                   (addonLines.Count > 0 ? $"\n<b>Дополнения:</b>\n{string.Join("\n", addonLines)}\n" : "") +
                   $"\n<b>Итого:</b> {totalPrice:F2}₽\n" +
                   $"Дата: {order.CreatedAt:dd.MM.yyyy HH:mm}";

        var canEdit = project?.Status?.CanEditOrders == true;
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
