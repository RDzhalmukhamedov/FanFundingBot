using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Core.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class BroadcastTextHandler : IBroadcastTextHandler
{
    private readonly IUserStateService _stateService;

    public BroadcastTextHandler(IUserStateService stateService)
    {
        _stateService = stateService;
    }

    public async Task SendBroadcastAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;

        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = null;
        session.TempBroadcastText = text;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            $"📢 <b>Подтверждение рассылки</b>\n\n{text}\n\nОтправить это сообщение всем подписанным пользователям?",
            parseMode: ParseMode.Html,
            replyMarkup: ConfirmationKeyboard.CreateBroadcastConfirmation(),
            cancellationToken: ct);
    }
}