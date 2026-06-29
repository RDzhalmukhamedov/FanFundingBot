using FanFundingBot.Bot.Abstractions;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Handlers;

public class ContactHandler : IContactHandler
{
    private readonly ICommandHandler _commandHandler;

    public ContactHandler(ICommandHandler commandHandler)
    {
        _commandHandler = commandHandler;
    }

    public async Task HandleContactAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var contact = message.Contact!;
        var phone = contact.PhoneNumber ?? string.Empty;

        if (!phone.StartsWith("+"))
        {
            phone = "+" + phone;
        }

        await _commandHandler.HandlePhoneContactAsync(botClient, message, phone, ct);
    }
}
