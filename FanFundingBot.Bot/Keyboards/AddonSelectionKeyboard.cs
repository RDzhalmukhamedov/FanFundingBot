using FanFundingBot.Bot.Helpers;
using FanFundingBot.Domain.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

public static class AddonSelectionKeyboard
{
    private const int PageSize = 6;

    public static (InlineKeyboardMarkup Keyboard, int TotalPages) Create(
        List<Addon> addons,
        HashSet<Guid> selectedIds,
        int page,
        int totalCount)
    {
        var totalPages = (int)Math.Ceiling((double)totalCount / PageSize);
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var addon in addons)
        {
            var isSelected = selectedIds.Contains(addon.Id);
            var checkmark = isSelected ? "✅ " : "";
            var priceStr = addon.Price.ToString();
            var buttonText = $"{checkmark}{addon.Name} — {priceStr}₽";

            buttons.Add(new List<InlineKeyboardButton>
            {
                InlineKeyboardButton.WithCallbackData(
                    buttonText,
                    CallbackData.Build("addon_toggle", addon.Id))
            });
        }

        var navRow = new List<InlineKeyboardButton>();
        if (page > 1)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("◀️ Назад", CallbackData.Build("addon_page", page - 1)));
        }

        navRow.Add(InlineKeyboardButton.WithCallbackData(
            $"{page}/{totalPages}", CallbackData.Build("addon_page", "nop")));

        if (page < totalPages)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("Вперёд ▶️", CallbackData.Build("addon_page", page + 1)));
        }

        buttons.Add(navRow);

        buttons.Add(new List<InlineKeyboardButton>
        {
            InlineKeyboardButton.WithCallbackData("✅ Подтвердить выбор", "addon_confirm"),
            InlineKeyboardButton.WithCallbackData("❌ Отменить", "order_cancel")
        });

        return (new InlineKeyboardMarkup(buttons), totalPages);
    }
}
