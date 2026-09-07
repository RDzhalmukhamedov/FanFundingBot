using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Domain.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class AdminQuestionCallbackHandler : IAdminQuestionCallbackHandler
{
    private readonly IUserStateService _stateService;
    private readonly IProjectRepository _projectRepository;
    private readonly IAdminService _adminService;
    private readonly MainMenuHelper _menuHelper;

    public AdminQuestionCallbackHandler(
        IUserStateService stateService,
        IProjectRepository projectRepository,
        IAdminService adminService,
        MainMenuHelper menuHelper)
    {
        _stateService = stateService;
        _projectRepository = projectRepository;
        _adminService = adminService;
        _menuHelper = menuHelper;
    }

    public async Task HandleAdminSelectQuestionAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var questionId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "answer_question";
        session.PendingQuestionId = questionId;
        _stateService.Update(userId, session);

        var questions = await _adminService.GetUnansweredQuestionsAsync();
        var selected = questions.FirstOrDefault(q => q.Id == questionId);

        var usernameLine = selected?.User.Username != null
            ? $"👤 @{selected.User.Username}\n\n"
            : "";

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"📝 <b>Вопрос от {selected?.User.FirstName ?? "пользователя"}</b>\n\n" +
            usernameLine +
            $"{selected?.Text}\n\n" +
            "Введите ваш ответ:",
            parseMode: ParseMode.Html,
            cancellationToken: ct);
    }

    public async Task HandleAdminBroadcastConfirmAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        var session = _stateService.Get(userId);
        if (session?.TempBroadcastText == null)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Текст сообщения не найден. Попробуйте начать заново.",
                cancellationToken: ct);
            return;
        }

        var text = session.TempBroadcastText;
        session.PendingAction = null;
        session.TempBroadcastText = null;
        _stateService.Update(userId, session);

        var projects = await _projectRepository.GetAllAsync();
        if (projects.Count == 0)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Нет проектов для рассылки.",
                cancellationToken: ct);
            return;
        }

        var result = await _adminService.BroadcastMessageAsync(projects.First().Id, text);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result,
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }

    public async Task HandleAdminCancelAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, CancellationToken ct)
    {
        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            "❌ Действие отменено.",
            cancellationToken: ct);

        await _menuHelper.SendReturnToMenuAsync(botClient, query.Message.Chat.Id, userId, ct);
    }
}