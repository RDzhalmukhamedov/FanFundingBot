using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminProjectCreationTextHandler : IAdminProjectCreationTextHandler
{
    private readonly IUserStateService _stateService;

    public AdminProjectCreationTextHandler(IUserStateService stateService)
    {
        _stateService = stateService;
    }

    public async Task HandleCreateProjectNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempProjectName = message.Text;
        session.PendingAction = "create_project_desc";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите описание проекта:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleCreateProjectDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempProjectDescription = message.Text;
        session.PendingAction = null;
        _stateService.Update(userId, session);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить дополнение", "admin_add_create_addon") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Создать проект", "admin_create_project_confirm") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin_cancel") }
        });

        var addonsSummary = session.TempAddons == null || session.TempAddons.Count == 0
            ? "Нет дополнений"
            : string.Join("\n", session.TempAddons.Select(a => $"  • {a.Name} — {a.Price:F2}₽"));

        await botClient.SendMessage(
            message.Chat.Id,
            $"📋 <b>Новый проект: {session.TempProjectName}</b>\n\n" +
            $"<b>Описание:</b> {session.TempProjectDescription}\n\n" +
            $"<b>Дополнения:</b>\n{addonsSummary}",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }

    public async Task HandleCreateAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonName = message.Text;
        session.PendingAction = "create_addon_desc";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите описание дополнения:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleCreateAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonDescription = message.Text;
        session.PendingAction = "create_addon_price";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите цену дополнения (например: 499.99):",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleCreateAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var priceError = InputValidation.ValidatePrice(message.Text, out var price);
        if (priceError != null)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                priceError,
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        session.TempAddons ??= new List<(string Name, string Description, decimal Price)>();
        session.TempAddons.Add((session.TempAddonName!, session.TempAddonDescription ?? "", price));
        session.TempAddonName = null;
        session.TempAddonDescription = null;
        session.PendingAction = null;
        _stateService.Update(userId, session);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить дополнение", "admin_add_create_addon") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Создать проект", "admin_create_project_confirm") },
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "admin_cancel") }
        });

        var addonsSummary = session.TempAddons.Count == 0
            ? "Нет дополнений"
            : string.Join("\n", session.TempAddons.Select(a => $"  • {a.Name} — {a.Price:F2}₽"));

        await botClient.SendMessage(
            message.Chat.Id,
            $"📋 <b>Новый проект: {session.TempProjectName}</b>\n\n" +
            $"<b>Описание:</b> {session.TempProjectDescription}\n\n" +
            $"<b>Дополнения:</b>\n{addonsSummary}",
            parseMode: ParseMode.Html,
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}