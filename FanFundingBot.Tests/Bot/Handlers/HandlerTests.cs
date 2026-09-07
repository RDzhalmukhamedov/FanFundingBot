using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Handlers;

internal static class TelegramFactory
{
    public static Message CreateMessage(string? text = null, long userId = 1, string? contactPhone = null)
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 1);
        SetProp(msg, "Chat", CreateChat(1));
        SetProp(msg, "From", CreateUser(userId, "Test"));
        if (text != null) SetProp(msg, "Text", text);
        if (contactPhone != null) SetProp(msg, "Contact", CreateContact(contactPhone));
        return msg;
    }

    public static Chat CreateChat(long id)
    {
        var chat = new Chat();
        SetProp(chat, "Id", id);
        return chat;
    }

    public static User CreateUser(long id, string firstName)
    {
        var user = new User();
        SetProp(user, "Id", id);
        SetProp(user, "FirstName", firstName);
        return user;
    }

    public static Contact CreateContact(string phoneNumber)
    {
        var contact = new Contact();
        SetProp(contact, "PhoneNumber", phoneNumber);
        return contact;
    }

    public static CallbackQuery CreateCallbackQuery(string id, string data, long userId, Message? message = null)
    {
        var cq = new CallbackQuery();
        SetProp(cq, "Id", id);
        SetProp(cq, "Data", data);
        SetProp(cq, "From", CreateUser(userId, "Test"));
        if (message != null) SetProp(cq, "Message", message);
        return cq;
    }

    public static Update CreateUpdate(Message? message = null, CallbackQuery? callbackQuery = null)
    {
        var update = new Update();
        SetProp(update, "Id", 1);
        if (message != null) SetProp(update, "Message", message);
        if (callbackQuery != null) SetProp(update, "CallbackQuery", callbackQuery);
        return update;
    }

    private static void SetProp<T>(T obj, string propName, object value)
    {
        var prop = typeof(T).GetProperty(propName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        var setMethod = prop?.GetSetMethod(nonPublic: true);
        setMethod?.Invoke(obj, [value]);
    }
}

[TestFixture]
public class UpdateHandlerTests
{
    [Test]
    public async Task HandleUpdateAsync_ShouldRouteToCommandHandler()
    {
        var commandHandlerMock = CreateCommandHandlerMock();
        var callbackHandlerMock = CreateCallbackHandlerMock();
        var contactHandlerMock = new Mock<IContactHandler>();

        var updateHandler = new UpdateHandler(
            commandHandlerMock.Object,
            callbackHandlerMock.Object,
            contactHandlerMock.Object);

        var botClientMock = new Mock<ITelegramBotClient>();

        commandHandlerMock
            .Setup(h => h.HandleCommandAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var update = TelegramFactory.CreateUpdate(
            message: TelegramFactory.CreateMessage(text: "/start"));

        await updateHandler.HandleUpdateAsync(botClientMock.Object, update, CancellationToken.None);

        commandHandlerMock.Verify(
            h => h.HandleCommandAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task HandleUpdateAsync_CallbackQuery_ShouldRouteToCallbackHandler()
    {
        var commandHandlerMock = CreateCommandHandlerMock();
        var callbackHandlerMock = CreateCallbackHandlerMock();
        var contactHandlerMock = new Mock<IContactHandler>();

        var updateHandler = new UpdateHandler(
            commandHandlerMock.Object,
            callbackHandlerMock.Object,
            contactHandlerMock.Object);

        var botClientMock = new Mock<ITelegramBotClient>();

        callbackHandlerMock
            .Setup(h => h.HandleCallbackQueryAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<CallbackQuery>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cq = TelegramFactory.CreateCallbackQuery("1", "test_data", 1,
            TelegramFactory.CreateMessage());
        var update = TelegramFactory.CreateUpdate(callbackQuery: cq);

        await updateHandler.HandleUpdateAsync(botClientMock.Object, update, CancellationToken.None);

        callbackHandlerMock.Verify(
            h => h.HandleCallbackQueryAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<CallbackQuery>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task HandleUpdateAsync_Contact_ShouldRouteToContactHandler()
    {
        var commandHandlerMock = CreateCommandHandlerMock();
        var callbackHandlerMock = CreateCallbackHandlerMock();
        var contactHandlerMock = new Mock<IContactHandler>();

        var updateHandler = new UpdateHandler(
            commandHandlerMock.Object,
            callbackHandlerMock.Object,
            contactHandlerMock.Object);

        var botClientMock = new Mock<ITelegramBotClient>();

        contactHandlerMock
            .Setup(h => h.HandleContactAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var update = TelegramFactory.CreateUpdate(
            message: TelegramFactory.CreateMessage(contactPhone: "+71234567890"));

        await updateHandler.HandleUpdateAsync(botClientMock.Object, update, CancellationToken.None);

        contactHandlerMock.Verify(
            h => h.HandleContactAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static Mock<ICommandHandler> CreateCommandHandlerMock()
    {
        return new Mock<ICommandHandler>();
    }

    private static Mock<ICallbackQueryHandler> CreateCallbackHandlerMock()
    {
        return new Mock<ICallbackQueryHandler>();
    }
}

[TestFixture]
public class ContactHandlerTests
{
    [Test]
    public async Task HandleContactAsync_ShouldAddPlusPrefix()
    {
        var commandHandlerMock = new Mock<ICommandHandler>();

        var handler = new ContactHandler(commandHandlerMock.Object);
        var botClientMock = new Mock<ITelegramBotClient>();

        commandHandlerMock
            .Setup(h => h.HandlePhoneContactAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.Is<string>(p => p == "+71234567890"),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var msg = TelegramFactory.CreateMessage(contactPhone: "71234567890");

        await handler.HandleContactAsync(botClientMock.Object, msg, CancellationToken.None);

        commandHandlerMock.Verify(
            h => h.HandleCommandAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        commandHandlerMock.Verify(
            h => h.HandlePhoneContactAsync(
                It.IsAny<ITelegramBotClient>(),
                It.IsAny<Message>(),
                "+71234567890",
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

[TestFixture]
public class CommandHandlerBroadcastTests
{
    [Test]
    public async Task BroadcastMessage_SetsTempBroadcastTextAndShowsConfirmation()
    {
        var userRepoMock = new Mock<IUserRepository>();
        var projectRepoMock = new Mock<IProjectRepository>();
        var stateService = new UserStateService(new MemoryCache(new MemoryCacheOptions()));
        var adminServiceMock = new Mock<IAdminService>();
        var orderFlowServiceMock = new Mock<IOrderFlowService>();
        var config = new BotConfiguration { OwnerId = 1 };
        var uiRendererMock = new Mock<IUIRenderer>();
        var questionRepoMock = new Mock<IQuestionRepository>();
        var addonRepoMock = new Mock<IAddonRepository>();
        var loggerMock = new Mock<ILogger<BroadcastTextHandler>>();

        var questionHandler = new QuestionTextHandler(stateService, questionRepoMock.Object, userRepoMock.Object, adminServiceMock.Object);
        var broadcastHandler = new BroadcastTextHandler(stateService);
        var orderHandler = new OrderTextHandler(stateService, orderFlowServiceMock.Object, uiRendererMock.Object);
        var creationHandler = new AdminProjectCreationTextHandler(stateService, orderFlowServiceMock.Object);
        var editingHandler = new AdminProjectEditingTextHandler(stateService, adminServiceMock.Object, addonRepoMock.Object,
            new ProjectEditFormBuilder(adminServiceMock.Object, addonRepoMock.Object));

        var textInputHandler = new TextInputHandler(
            stateService, adminServiceMock.Object,
            questionHandler, broadcastHandler, orderHandler, creationHandler, editingHandler);

        adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        userRepoMock.Setup(r => r.ExistsAsync(It.IsAny<long>())).ReturnsAsync(true);

        var menuHelper = new MainMenuHelper(adminServiceMock.Object);
        var adminCommandHandler = new AdminCommandHandler(stateService, adminServiceMock.Object, projectRepoMock.Object, questionRepoMock.Object, menuHelper);
        var handler = new CommandHandler(
            userRepoMock.Object, projectRepoMock.Object, stateService,
            adminServiceMock.Object, orderFlowServiceMock.Object,
            config, uiRendererMock.Object, textInputHandler, menuHelper, adminCommandHandler);

        var botClientMock = new Mock<ITelegramBotClient>();
        var msg = TelegramFactory.CreateMessage(text: "📢 Рассылка");

        await handler.HandleCommandAsync(botClientMock.Object, msg, CancellationToken.None);

        var msg2 = TelegramFactory.CreateMessage(text: "Hello everyone!");
        await handler.HandleCommandAsync(botClientMock.Object, msg2, CancellationToken.None);

        var session = stateService.Get(1);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.TempBroadcastText, Is.EqualTo("Hello everyone!"));
        Assert.That(session.PendingAction, Is.Null);
    }
}