using System.Reflection;
using FanFundingBot.Application.Questions.Commands;
using FanFundingBot.Application.Questions.DTOs;
using FanFundingBot.Application.Questions.Queries;
using FanFundingBot.Application.Users.Commands;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FluentAssertions;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Requests;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using ApplicationAbstractions = FanFundingBot.Application.Abstractions;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class QuestionTextHandlerTests
{
    private IUserStateService _stateService;
    private IMediator _mediator;
    private ITelegramBotClient _botClient;
    private QuestionTextHandler _handler;
    private UserSession _session;

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();
        _stateService = Substitute.For<IUserStateService>();
        var authorization = Substitute.For<ApplicationAbstractions.IAuthorizationService>();
        authorization.CanCreateProjectAsync(Arg.Any<long>()).Returns(false);
        _handler = new QuestionTextHandler(_stateService, _mediator, new MainMenuHelper(authorization));
        _botClient = Substitute.For<ITelegramBotClient>();
        _session = new UserSession { PendingAction = "ask_question" };
    }

    private static Message MakeMessage(string text, long userId = 1)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 1);
        SetProp(msg, "Chat", new Chat { Id = 200 });
        SetProp(msg, "From", new Telegram.Bot.Types.User { Id = userId, FirstName = "Test" });
        SetProp(msg, "Text", text);
        return msg;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    private static bool IsMainMenu(ReplyMarkup? markup)
    {
        return markup is ReplyKeyboardMarkup kb
            && kb.Keyboard!.SelectMany(row => row).Any(b => b.Text == "❓ Задать вопрос");
    }

    [Test]
    public async Task SaveQuestion_Success_ReturnsMainMenu()
    {
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<AskQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(Guid.NewGuid()));

        await _handler.SaveQuestionAsync(_botClient, MakeMessage("Мой вопрос?"), CancellationToken.None);

        Assert.That(_session.PendingAction, Is.Null);
        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("Ваш вопрос принят") && IsMainMenu(r.ReplyMarkup)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveQuestion_Failure_StillReturnsMainMenu()
    {
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<AskQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid>("Ошибка сохранения."));

        await _handler.SaveQuestionAsync(_botClient, MakeMessage("Мой вопрос?"), CancellationToken.None);

        Assert.That(_session.PendingAction, Is.Null);
        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("Ошибка сохранения.") && IsMainMenu(r.ReplyMarkup)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAnswer_Success_ReturnsMainMenu()
    {
        var qId = Guid.NewGuid();
        _session.PendingAction = "answer_question";
        _session.PendingQuestionId = qId;
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<AnswerQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        await _handler.SaveAnswerAsync(_botClient, MakeMessage("Ответ."), qId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.PendingAction, Is.Null);
            Assert.That(_session.PendingQuestionId, Is.Null);
        });
        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("Ответ отправлен") && IsMainMenu(r.ReplyMarkup)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task SaveAnswer_Failure_StillReturnsMainMenu()
    {
        var qId = Guid.NewGuid();
        _session.PendingAction = "answer_question";
        _session.PendingQuestionId = qId;
        _stateService.Get(1).Returns(_session);
        _mediator.Send(Arg.Any<AnswerQuestionCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Нет прав."));

        await _handler.SaveAnswerAsync(_botClient, MakeMessage("Ответ."), qId, CancellationToken.None);

        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r =>
                r.Text != null && r.Text.Contains("Нет прав.") && IsMainMenu(r.ReplyMarkup)),
            Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class AdminAnswerCancelTests
{
    private IMediator _mediator;
    private IUserStateService _stateService;
    private ITelegramBotClient _botClient;
    private UserSession _session;

    [SetUp]
    public void Setup()
    {
        _mediator = Substitute.For<IMediator>();
        _stateService = Substitute.For<IUserStateService>();
        _botClient = Substitute.For<ITelegramBotClient>();
        _session = new UserSession();
    }

    private static CallbackQuery MakeCq(string data)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 100);
        SetProp(msg, "Chat", new Chat { Id = 200 });
        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", data);
        SetProp(cq, "Message", msg);
        SetProp(cq, "From", new Telegram.Bot.Types.User { Id = 1, FirstName = "Admin" });
        return cq;
    }

    private static Message MakeMessage(string text, long userId = 1)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 1);
        SetProp(msg, "Chat", new Chat { Id = 200 });
        SetProp(msg, "From", new Telegram.Bot.Types.User { Id = userId, FirstName = "Admin" });
        SetProp(msg, "Text", text);
        return msg;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    private static bool HasBackButton(ReplyMarkup? markup)
    {
        return markup is ReplyKeyboardMarkup kb
            && kb.Keyboard!.SelectMany(row => row).Any(b => b.Text == "◀️ Назад");
    }

    [Test]
    public async Task SelectQuestion_ShowsBackKeyboard()
    {
        var qId = Guid.NewGuid();
        var authorization = Substitute.For<ApplicationAbstractions.IAuthorizationService>();
        var handler = new AdminQuestionCallbackHandler(_stateService, _mediator, new MainMenuHelper(authorization));
        _mediator.Send(Arg.Any<GetUnansweredQuestionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(new FanFundingBot.Application.Common.Models.PagedResult<QuestionDto>(
                new List<QuestionDto>
                {
                    new(qId, "Q?", 5, "testuser", "User1", DateTime.UtcNow, null)
                }, 1, 1, 20)));
        _stateService.GetOrCreate(1).Returns(_session);

        await handler.HandleAdminSelectQuestionAsync(_botClient, MakeCq("admin_answer:" + qId), 1,
            "admin_answer:" + qId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(_session.PendingAction, Is.EqualTo("answer_question"));
            Assert.That(_session.PendingQuestionId, Is.EqualTo(qId));
        });
        await _botClient.Received(1).SendRequest<Message>(
            Arg.Is<SendMessageRequest>(r => HasBackButton(r.ReplyMarkup)),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Back_DuringAnswerInput_CancelsWithoutSaving()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        try
        {
            var realState = new UserStateService(cache);
            var authorization = Substitute.For<ApplicationAbstractions.IAuthorizationService>();
            authorization.CanCreateProjectAsync(Arg.Any<long>()).Returns(true);
            var menuHelper = new MainMenuHelper(authorization);
            var uiRenderer = Substitute.For<IUIRenderer>();
            var questionHandler = new QuestionTextHandler(realState, _mediator, menuHelper);
            var broadcastHandler = new BroadcastTextHandler(realState);
            var orderHandler = new OrderTextHandler(realState, uiRenderer);
            var creationHandler = new AdminProjectCreationTextHandler(realState);
            var editingHandler = new AdminProjectEditingTextHandler(realState, _mediator,
                new ProjectEditFormBuilder(_mediator));
            var textInputHandler = new TextInputHandler(
                realState, authorization,
                questionHandler, broadcastHandler, orderHandler, creationHandler, editingHandler);
            var adminCommandHandler = new AdminCommandHandler(realState, _mediator, menuHelper);
            var handler = new CommandHandler(
                realState, authorization, _mediator, new TelegramUserContext(),
                new BotConfiguration { OwnerId = 1 }, uiRenderer, textInputHandler, menuHelper, adminCommandHandler);

            _mediator.Send(Arg.Any<GetOrCreateUserCommand>(), Arg.Any<CancellationToken>())
                .Returns(Result.Ok());

            var session = realState.GetOrCreate(1);
            session.PendingAction = "answer_question";
            session.PendingQuestionId = Guid.NewGuid();
            realState.Update(1, session);

            await handler.HandleCommandAsync(_botClient, MakeMessage("◀️ Назад"), CancellationToken.None);

            await _mediator.DidNotReceive().Send(
                Arg.Any<AnswerQuestionCommand>(), Arg.Any<CancellationToken>());
            realState.Get(1).Should().BeNull();
            await _botClient.Received(1).SendRequest<Message>(
                Arg.Is<SendMessageRequest>(r => r.Text != null && r.Text.Contains("Ввод ответа отменён")),
                Arg.Any<CancellationToken>());
            await _botClient.Received(1).SendRequest<Message>(
                Arg.Is<SendMessageRequest>(r => r.Text == "Возвращаюсь в главное меню."),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            cache.Dispose();
        }
    }
}
