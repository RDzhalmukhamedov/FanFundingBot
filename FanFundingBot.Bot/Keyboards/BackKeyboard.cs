using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

[ExcludeFromCodeCoverage]
public static class BackKeyboard
{
    public static ReplyKeyboardMarkup Create(string backButtonText = "◀️ Назад")
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton(backButtonText)
        })
        {
            ResizeKeyboard = true
        };
    }
}
