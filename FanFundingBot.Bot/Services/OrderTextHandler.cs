using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class OrderTextHandler : IOrderTextHandler
{
    private readonly IUserStateService _stateService;
    private readonly IUIRenderer _uiRenderer;

    public OrderTextHandler(
        IUserStateService stateService,
        IUIRenderer uiRenderer)
    {
        _stateService = stateService;
        _uiRenderer = uiRenderer;
    }

    public async Task HandlePhoneInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var phone = message.Text!;

        var error = InputValidation.ValidatePhone(phone);
        if (error != null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                error,
                replyMarkup: new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null) return;

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

    public async Task HandleAddressInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var address = message.Text!;

        var error = InputValidation.ValidateAddress(address);
        if (error != null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                error,
                replyMarkup: new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var session = _stateService.Get(userId);
        if (session == null) return;

        session.Address = address;
        session.State = OrderFlowState.Confirming;
        _stateService.Update(userId, session);

        await _uiRenderer.ShowConfirmationAsync(botClient, message.Chat.Id, userId, ct);
    }
}
