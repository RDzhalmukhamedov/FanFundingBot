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

        var cleaned = System.Text.RegularExpressions.Regex.Replace(phone, @"[\s\-\(\)\.]", "");
        if (cleaned.Length == 11 && cleaned[0] == '8' && cleaned.All(char.IsDigit))
            phone = "+7" + cleaned[1..];
        else if (cleaned.Length == 11 && cleaned[0] == '7' && cleaned.All(char.IsDigit))
            phone = "+" + cleaned;
        else
            phone = FanFundingBot.Domain.Extensions.StringExtensions.NormalizePhone(phone) ?? phone.Trim();

        await _commandHandler.HandlePhoneContactAsync(botClient, message, phone, ct);
    }
}
