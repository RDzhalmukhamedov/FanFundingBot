using System.Diagnostics.CodeAnalysis;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Keyboards;

[ExcludeFromCodeCoverage]
public static class MainMenuKeyboard
{
    public static ReplyKeyboardMarkup Create(bool isAdmin = false)
    {
        var buttons = new List<List<KeyboardButton>>
        {
            new() { new KeyboardButton("📋 Статус проекта"), new KeyboardButton("📦 Мой заказ") },
            new() { new KeyboardButton("🛒 Оформить заказ"), new KeyboardButton("❓ Задать вопрос") },
            new() { new KeyboardButton("ℹ️ О проекте") }
        };

        if (isAdmin)
        {
            buttons.Add(new List<KeyboardButton>
            {
                new KeyboardButton("🔄 Обновить статус"),
                new KeyboardButton("📢 Рассылка")
            });
            buttons.Add(new List<KeyboardButton>
            {
                new KeyboardButton("✉️ Ответить на вопрос")
            });
            buttons.Add(new List<KeyboardButton>
            {
                new KeyboardButton("➕ Создать проект"),
                new KeyboardButton("✏️ Редактировать проект")
            });
        }

        return new ReplyKeyboardMarkup(buttons)
        {
            ResizeKeyboard = true
        };
    }
}
