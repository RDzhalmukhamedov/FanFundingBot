using FanFundingBot.Bot.Helpers;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

public static class DeliveryMethodKeyboard
{
    public static InlineKeyboardMarkup Create()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🚶 Самовывоз", CallbackData.Build("delivery", "pickup")),
                InlineKeyboardButton.WithCallbackData("🚚 Доставка", CallbackData.Build("delivery", "delivery"))
            }
        });
    }
}
