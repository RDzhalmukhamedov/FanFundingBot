using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Bot.Services;

public class AdminProjectEditingTextHandler : IAdminProjectEditingTextHandler
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;
    private readonly IProjectEditFormBuilder _editFormBuilder;

    public AdminProjectEditingTextHandler(
        IUserStateService stateService,
        IMediator mediator,
        IProjectEditFormBuilder editFormBuilder)
    {
        _stateService = stateService;
        _mediator = mediator;
        _editFormBuilder = editFormBuilder;
    }

    public async Task HandleEditProjectNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projectId = Guid.Parse(session.PendingAction!["edit_project_name:".Length..]);
        var result = await _mediator.Send(new RenameProjectCommand(userId, projectId, message.Text!), ct);

        session.PendingAction = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? $"✅ Название проекта изменено на «{message.Text}»." : result.Errors[0].Message,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }

    public async Task HandleEditProjectDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var projectId = Guid.Parse(session.PendingAction!["edit_project_desc:".Length..]);
        var result = await _mediator.Send(new UpdateProjectDescriptionCommand(userId, projectId, message.Text!), ct);

        session.PendingAction = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? "✅ Описание проекта обновлено." : result.Errors[0].Message,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }

    public async Task HandleEditAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var addonId = Guid.Parse(session.PendingAction!["edit_addon_name:".Length..]);
        var result = await _mediator.Send(new UpdateAddonNameCommand(userId, addonId, message.Text!), ct);

        session.PendingAction = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? $"✅ Название дополнения изменено на «{message.Text}»." : result.Errors[0].Message,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }

    public async Task HandleEditAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var addonId = Guid.Parse(session.PendingAction!["edit_addon_desc:".Length..]);
        var result = await _mediator.Send(new UpdateAddonDescriptionCommand(userId, addonId, message.Text!), ct);

        session.PendingAction = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? "✅ Описание дополнения обновлено." : result.Errors[0].Message,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }

    public async Task HandleEditAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        if (!decimal.TryParse(message.Text, out var price) || price < 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Некорректная цена. Введите число (например: 499.99):",
                replyMarkup: BackKeyboard.Create(),
                cancellationToken: ct);
            return;
        }

        var addonId = Guid.Parse(session.PendingAction!["edit_addon_price:".Length..]);
        var result = await _mediator.Send(new UpdateAddonPriceCommand(userId, addonId, price), ct);

        session.PendingAction = null;
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? $"✅ Цена дополнения изменена на {price:F2}₽." : result.Errors[0].Message,
            replyMarkup: MainMenuKeyboard.Create(true),
            cancellationToken: ct);
    }

    public async Task HandleWizardAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonName = message.Text;
        session.PendingAction = $"wizard_addon_desc:{session.TempAddonId:N}";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Шаг 2 из 3. Введите новое описание дополнения:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleWizardAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonDescription = message.Text;
        session.PendingAction = $"wizard_addon_price:{session.TempAddonId:N}";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Шаг 3 из 3. Введите новую цену дополнения (например: 499.99):",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleWizardAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        if (!decimal.TryParse(message.Text, out var price) || price < 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Некорректная цена. Введите число (например: 499.99):",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var addonId = session.TempAddonId!.Value;
        var projectResult = await _mediator.Send(new GetProjectIdByAddonQuery(addonId), ct);
        if (projectResult.IsFailed)
        {
            await botClient.SendMessage(message.Chat.Id, "Дополнение не найдено.", cancellationToken: ct);
            return;
        }

        var projectId = projectResult.Value;
        await _mediator.Send(new UpdateAddonNameCommand(userId, addonId, session.TempAddonName!), ct);
        await _mediator.Send(new UpdateAddonDescriptionCommand(userId, addonId, session.TempAddonDescription ?? ""), ct);
        var priceResult = await _mediator.Send(new UpdateAddonPriceCommand(userId, addonId, price), ct);

        session.TempAddonName = null;
        session.TempAddonDescription = null;
        session.TempAddonId = null;
        session.PendingAction = null;
        _stateService.Update(userId, session);

        try
        {
            var form = await _editFormBuilder.BuildAsync(projectId, "✅ Дополнение обновлено.");
            await botClient.SendMessage(message.Chat.Id, form.Text, parseMode: ParseMode.Html, replyMarkup: form.Keyboard, cancellationToken: ct);
        }
        catch (InvalidOperationException)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                priceResult.IsSuccess ? "✅ Дополнение обновлено." : priceResult.Errors[0].Message,
                replyMarkup: MainMenuKeyboard.Create(true),
                cancellationToken: ct);
        }
    }

    public async Task HandleAddAddonNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonName = message.Text;
        session.PendingAction = $"add_addon_desc:{session.TempProjectId:N}";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите описание дополнения:",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleAddAddonDescAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempAddonDescription = message.Text;
        session.PendingAction = $"add_addon_price:{session.TempProjectId:N}";
        _stateService.Update(userId, session);

        await botClient.SendMessage(
            message.Chat.Id,
            "Введите цену дополнения (например: 499.99):",
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: ct);
    }

    public async Task HandleAddAddonPriceAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        if (!decimal.TryParse(message.Text, out var price) || price < 0)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Некорректная цена. Введите число (например: 499.99):",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct);
            return;
        }

        var projectId = session.TempProjectId!.Value;
        var addonName = session.TempAddonName!;
        var result = await _mediator.Send(new CreateAddonCommand(
            userId, projectId, addonName, session.TempAddonDescription ?? "", price), ct);

        session.TempAddonName = null;
        session.TempAddonDescription = null;
        session.TempProjectId = null;
        session.PendingAction = null;
        _stateService.Update(userId, session);

        var resultText = result.IsSuccess
            ? $"✅ Дополнение «{addonName}» добавлено."
            : result.Errors[0].Message;

        try
        {
            var form = await _editFormBuilder.BuildAsync(projectId, resultText);
            await botClient.SendMessage(message.Chat.Id, form.Text, parseMode: ParseMode.Html, replyMarkup: form.Keyboard, cancellationToken: ct);
        }
        catch (InvalidOperationException)
        {
            await botClient.SendMessage(message.Chat.Id, resultText, replyMarkup: MainMenuKeyboard.Create(true), cancellationToken: ct);
        }
    }

    public async Task HandleAddStatusNameAsync(ITelegramBotClient botClient, Message message, UserSession session, CancellationToken ct)
    {
        var userId = message.From!.Id;
        session.TempStatusName = message.Text;
        session.PendingAction = null;
        _stateService.Update(userId, session);

        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Да", CallbackData.Build("admin_add_status_can_edit", "yes")),
                InlineKeyboardButton.WithCallbackData("❌ Нет", CallbackData.Build("admin_add_status_can_edit", "no"))
            }
        });

        await botClient.SendMessage(
            message.Chat.Id,
            "Должна ли быть доступна возможность редактирования и создания заказов для этого статуса?",
            replyMarkup: keyboard,
            cancellationToken: ct);
    }
}
