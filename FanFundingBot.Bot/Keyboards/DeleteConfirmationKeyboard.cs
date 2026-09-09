using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Bot.Helpers;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

[ExcludeFromCodeCoverage]
public static class DeleteConfirmationKeyboard
{
    public static InlineKeyboardMarkup Create(Guid orderId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    "✅ Да, удалить",
                    CallbackData.Build("order_delete_my_confirm", orderId.ToString("N"))),
                InlineKeyboardButton.WithCallbackData(
                    "❌ Отмена",
                    CallbackData.Build("order_delete_my_cancel", orderId.ToString("N")))
            }
        });
    }
}
