using System.Diagnostics.CodeAnalysis;
using FanFundingBot.Bot.Helpers;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

[ExcludeFromCodeCoverage]
public static class ConfirmationKeyboard
{
    public static InlineKeyboardMarkup Create(bool showEditButton = true)
    {
        var buttons = new List<InlineKeyboardButton[]>
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", "order_confirm"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Отменить", "order_cancel")
            }
        };

        if (showEditButton)
        {
            buttons.Insert(0, new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", "order_edit")
            });
        }

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup Create(List<(Guid Id, string Name, decimal Price, int Quantity)> addons, bool showEditButton = true)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        foreach (var (id, name, _, qty) in addons)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("➖", CallbackData.Build("addon_qty_dec", id)),
                InlineKeyboardButton.WithCallbackData(
                    $"{qty}x {name}", "nop"),
                InlineKeyboardButton.WithCallbackData("➕", CallbackData.Build("addon_qty_inc", id))
            });
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("✅ Подтвердить", "order_confirm"),
        });

        if (showEditButton)
        {
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", "order_edit")
            });
        }

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("❌ Отменить", "order_cancel")
        });

        return new InlineKeyboardMarkup(buttons);
    }

    public static InlineKeyboardMarkup CreateBroadcastConfirmation()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Отправить", "admin_broadcast_confirm") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin_cancel") }
        });
    }

    public static InlineKeyboardMarkup CreateStatusConfirmation(Guid projectId, int statusId)
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", CallbackData.Build("admin_status_confirm", projectId, statusId)),
                InlineKeyboardButton.WithCallbackData("❌ Отмена", CallbackData.Build("admin_status_cancel", projectId))
            }
        });
    }
}