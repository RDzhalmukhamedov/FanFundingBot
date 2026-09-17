using FanFundingBot.Application.Questions.Commands;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using MediatR;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Bot.Services;

public class QuestionTextHandler : IQuestionTextHandler
{
    private readonly IUserStateService _stateService;
    private readonly IMediator _mediator;
    private readonly MainMenuHelper _menuHelper;

    public QuestionTextHandler(
        IUserStateService stateService,
        IMediator mediator,
        MainMenuHelper menuHelper)
    {
        _stateService = stateService;
        _mediator = mediator;
        _menuHelper = menuHelper;
    }

    public async Task SaveQuestionAsync(ITelegramBotClient botClient, Message message, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;

        var result = await _mediator.Send(new AskQuestionCommand(userId, text), ct);

        var session = _stateService.Get(userId);
        if (session != null)
        {
            session.PendingAction = null;
            _stateService.Update(userId, session);
        }

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess
                ? "✅ Ваш вопрос принят! Мы ответим вам в ближайшее время."
                : result.Errors[0].Message,
            replyMarkup: await _menuHelper.CreateKeyboardAsync(userId),
            cancellationToken: ct);
    }

    public async Task SaveAnswerAsync(ITelegramBotClient botClient, Message message, Guid questionId, CancellationToken ct)
    {
        var userId = message.From!.Id;
        var text = message.Text!;

        var result = await _mediator.Send(new AnswerQuestionCommand(userId, questionId, text), ct);

        var session = _stateService.Get(userId);
        if (session != null)
        {
            session.PendingAction = null;
            session.PendingQuestionId = null;
            _stateService.Update(userId, session);
        }

        await botClient.SendMessage(
            message.Chat.Id,
            result.IsSuccess ? "✅ Ответ отправлен пользователю." : result.Errors[0].Message,
            replyMarkup: await _menuHelper.CreateKeyboardAsync(userId),
            cancellationToken: ct);
    }
}
