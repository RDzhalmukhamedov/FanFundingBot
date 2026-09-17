using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Keyboards;
using FanFundingBot.Bot.Models;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class TextInputHandler : ITextInputHandler
{
    private readonly IUserStateService _stateService;
    private readonly IAuthorizationService _authorization;
    private readonly IQuestionTextHandler _questionHandler;
    private readonly IBroadcastTextHandler _broadcastHandler;
    private readonly IOrderTextHandler _orderHandler;
    private readonly IAdminProjectCreationTextHandler _creationHandler;
    private readonly IAdminProjectEditingTextHandler _editingHandler;

    public TextInputHandler(
        IUserStateService stateService,
        IAuthorizationService authorization,
        IQuestionTextHandler questionHandler,
        IBroadcastTextHandler broadcastHandler,
        IOrderTextHandler orderHandler,
        IAdminProjectCreationTextHandler creationHandler,
        IAdminProjectEditingTextHandler editingHandler)
    {
        _stateService = stateService;
        _authorization = authorization;
        _questionHandler = questionHandler;
        _broadcastHandler = broadcastHandler;
        _orderHandler = orderHandler;
        _creationHandler = creationHandler;
        _editingHandler = editingHandler;
    }

    public async Task HandleTextInputAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var session = _stateService.Get(userId);

        if (session?.PendingAction is { } action)
        {
            switch (action)
            {
                case "ask_question":
                    await _questionHandler.SaveQuestionAsync(botClient, message, ct);
                    return;

                case "broadcast":
                    await _broadcastHandler.SendBroadcastAsync(botClient, message, ct);
                    return;

                case "answer_question" when session.PendingQuestionId.HasValue:
                    await _questionHandler.SaveAnswerAsync(botClient, message, session.PendingQuestionId.Value, ct);
                    return;

                // Project creation wizard
                case "create_project_name":
                    await _creationHandler.HandleCreateProjectNameAsync(botClient, message, session, ct);
                    return;

                case "create_project_desc":
                    await _creationHandler.HandleCreateProjectDescAsync(botClient, message, session, ct);
                    return;

                case "create_addon_name":
                    await _creationHandler.HandleCreateAddonNameAsync(botClient, message, session, ct);
                    return;

                case "create_addon_desc":
                    await _creationHandler.HandleCreateAddonDescAsync(botClient, message, session, ct);
                    return;

                case "create_addon_price":
                    await _creationHandler.HandleCreateAddonPriceAsync(botClient, message, session, ct);
                    return;

                // Project editing text inputs
                case string s when s.StartsWith("edit_project_name:"):
                    await _editingHandler.HandleEditProjectNameAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("edit_project_desc:"):
                    await _editingHandler.HandleEditProjectDescAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("edit_addon_name:"):
                    await _editingHandler.HandleEditAddonNameAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("edit_addon_desc:"):
                    await _editingHandler.HandleEditAddonDescAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("edit_addon_price:"):
                    await _editingHandler.HandleEditAddonPriceAsync(botClient, message, session, ct);
                    return;

                // Addon wizard
                case string s when s.StartsWith("wizard_addon_name:"):
                    await _editingHandler.HandleWizardAddonNameAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("wizard_addon_desc:"):
                    await _editingHandler.HandleWizardAddonDescAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("wizard_addon_price:"):
                    await _editingHandler.HandleWizardAddonPriceAsync(botClient, message, session, ct);
                    return;

                // Add addon to existing project
                case string s when s.StartsWith("add_addon_name:"):
                    await _editingHandler.HandleAddAddonNameAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("add_addon_desc:"):
                    await _editingHandler.HandleAddAddonDescAsync(botClient, message, session, ct);
                    return;

                case string s when s.StartsWith("add_addon_price:"):
                    await _editingHandler.HandleAddAddonPriceAsync(botClient, message, session, ct);
                    return;

                // Add status
                case "add_status_name":
                    await _editingHandler.HandleAddStatusNameAsync(botClient, message, session, ct);
                    return;
            }
        }

        if (session?.State == OrderFlowState.EnteringPhone)
        {
            await _orderHandler.HandlePhoneInputAsync(botClient, message, ct);
            return;
        }

        if (session?.State == OrderFlowState.EnteringAddress)
        {
            await _orderHandler.HandleAddressInputAsync(botClient, message, ct);
            return;
        }

        if (session != null && session.State != OrderFlowState.Idle)
        {
            await botClient.SendMessage(
                message.Chat.Id,
                "Пожалуйста, используйте кнопки для навигации.",
                cancellationToken: ct);
            return;
        }

        await botClient.SendMessage(
            message.Chat.Id,
            "Пожалуйста, воспользуйтесь кнопками меню.",
            replyMarkup: MainMenuKeyboard.Create(await _authorization.CanCreateProjectAsync(userId)),
            cancellationToken: ct);
    }
}