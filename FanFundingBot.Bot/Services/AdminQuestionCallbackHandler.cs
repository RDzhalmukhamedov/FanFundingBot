using FanFundingBot.Application.Projects.Commands;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Keyboards;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace FanFundingBot.Bot.Services;

public class AdminQuestionCallbackHandler : IAdminQuestionCallbackHandler
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;
    private readonly MainMenuHelper _menuHelper;

    public AdminQuestionCallbackHandler(
        IUserStateService stateService,
        IMediator mediator,
        MainMenuHelper menuHelper)
    {
        _stateService = stateService;
        _mediator = mediator;
        _menuHelper = menuHelper;
    }

    public async Task HandleAdminSelectQuestionAsync(ITelegramBotClient botClient, CallbackQuery query, long userId, string data, CancellationToken ct)
    {
        var questionId = CallbackData.GuidArg(data);
        var session = _stateService.GetOrCreate(userId);
        session.PendingAction = "answer_question";
        session.PendingQuestionId = questionId;
        _stateService.Update(userId, session);

        var questionsResult = await _mediator.Send(new GetUnansweredQuestionsQuery(userId), ct);
        var questions = questionsResult.IsSuccess
            ? questionsResult.Value
            : new List<Application.Questions.DTOs.QuestionDto>();
        var selected = questions.FirstOrDefault(q => q.Id == questionId);

        var usernameLine = selected?.Username != null
            ? $"👤 @{selected.Username}\n\n"
            : "";

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message!.MessageId,
            $"📝 <b>Вопрос от {selected?.FirstName ?? "пользователя"}</b>\n\n" +
            usernameLine +
            $"{selected?.Text}\n\n" +
            "Введите ваш ответ:",
            parseMode: ParseMode.Html,
            cancellationToken: ct);

        await botClient.SendMessage(
            query.Message!.Chat.Id,
            "Введите ваш ответ или нажмите ◀️ Назад для отмены.",
            replyMarkup: BackKeyboard.Create(),
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

        var projectsResult = await _mediator.Send(new GetProjectsQuery(), ct);
        var projects = projectsResult.IsSuccess
            ? projectsResult.Value
            : new List<Application.Projects.DTOs.ProjectDto>();
        if (projects.Count == 0)
        {
            await botClient.EditMessageText(
                query.Message!.Chat.Id, query.Message.MessageId,
                "Нет проектов для рассылки.",
                cancellationToken: ct);
            return;
        }

        var result = await _mediator.Send(new BroadcastCommand(userId, projects.First().Id, text), ct);

        await botClient.EditMessageText(
            query.Message!.Chat.Id,
            query.Message.MessageId,
            result.IsSuccess
                ? "✅ Информационное сообщение отправлено всем подписанным пользователям."
                : result.Errors[0].Message,
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
