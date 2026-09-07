using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
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
        var commandHandler = Substitute.For<ICommandHandler>();
        var callbackHandler = Substitute.For<ICallbackQueryHandler>();
        var contactHandler = Substitute.For<IContactHandler>();

        var updateHandler = new UpdateHandler(commandHandler, callbackHandler, contactHandler);
        var botClient = Substitute.For<ITelegramBotClient>();

        var update = TelegramFactory.CreateUpdate(
            message: TelegramFactory.CreateMessage(text: "/start"));

        await updateHandler.HandleUpdateAsync(botClient, update, CancellationToken.None);

        await commandHandler.Received(1).HandleCommandAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleUpdateAsync_CallbackQuery_ShouldRouteToCallbackHandler()
    {
        var commandHandler = Substitute.For<ICommandHandler>();
        var callbackHandler = Substitute.For<ICallbackQueryHandler>();
        var contactHandler = Substitute.For<IContactHandler>();

        var updateHandler = new UpdateHandler(commandHandler, callbackHandler, contactHandler);
        var botClient = Substitute.For<ITelegramBotClient>();

        var cq = TelegramFactory.CreateCallbackQuery("1", "test_data", 1,
            TelegramFactory.CreateMessage());
        var update = TelegramFactory.CreateUpdate(callbackQuery: cq);

        await updateHandler.HandleUpdateAsync(botClient, update, CancellationToken.None);

        await callbackHandler.Received(1).HandleCallbackQueryAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<CallbackQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task HandleUpdateAsync_Contact_ShouldRouteToContactHandler()
    {
        var commandHandler = Substitute.For<ICommandHandler>();
        var callbackHandler = Substitute.For<ICallbackQueryHandler>();
        var contactHandler = Substitute.For<IContactHandler>();

        var updateHandler = new UpdateHandler(commandHandler, callbackHandler, contactHandler);
        var botClient = Substitute.For<ITelegramBotClient>();

        var update = TelegramFactory.CreateUpdate(
            message: TelegramFactory.CreateMessage(contactPhone: "+71234567890"));

        await updateHandler.HandleUpdateAsync(botClient, update, CancellationToken.None);

        await contactHandler.Received(1).HandleContactAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class ContactHandlerTests
{
    [Test]
    public async Task HandleContactAsync_ShouldAddPlusPrefix()
    {
        var commandHandler = Substitute.For<ICommandHandler>();

        var handler = new ContactHandler(commandHandler);
        var botClient = Substitute.For<ITelegramBotClient>();

        var msg = TelegramFactory.CreateMessage(contactPhone: "71234567890");

        await handler.HandleContactAsync(botClient, msg, CancellationToken.None);

        await commandHandler.DidNotReceive().HandleCommandAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<Message>(),
            Arg.Any<CancellationToken>());

        await commandHandler.Received(1).HandlePhoneContactAsync(
            Arg.Any<ITelegramBotClient>(),
            Arg.Any<Message>(),
            "+71234567890",
            Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class CommandHandlerBroadcastTests
{
    [Test]
    public async Task BroadcastMessage_SetsTempBroadcastTextAndShowsConfirmation()
    {
        var userRepo = Substitute.For<IUserRepository>();
        var projectRepo = Substitute.For<IProjectRepository>();
        var stateService = new UserStateService(new MemoryCache(new MemoryCacheOptions()));
        var adminService = Substitute.For<IAdminService>();
        var orderFlowService = Substitute.For<IOrderFlowService>();
        var config = new BotConfiguration { OwnerId = 1 };
        var uiRenderer = Substitute.For<IUIRenderer>();
        var questionRepo = Substitute.For<IQuestionRepository>();
        var addonRepo = Substitute.For<IAddonRepository>();

        var questionHandler = new QuestionTextHandler(stateService, questionRepo, userRepo, adminService);
        var broadcastHandler = new BroadcastTextHandler(stateService);
        var orderHandler = new OrderTextHandler(stateService, orderFlowService, uiRenderer);
        var creationHandler = new AdminProjectCreationTextHandler(stateService, orderFlowService);
        var editingHandler = new AdminProjectEditingTextHandler(stateService, adminService, addonRepo,
            new ProjectEditFormBuilder(adminService, addonRepo));

        var textInputHandler = new TextInputHandler(
            stateService, adminService,
            questionHandler, broadcastHandler, orderHandler, creationHandler, editingHandler);

        adminService.IsAdminAsync(1).Returns(true);
        userRepo.ExistsAsync(Arg.Any<long>()).Returns(true);

        var menuHelper = new MainMenuHelper(adminService);
        var adminCommandHandler = new AdminCommandHandler(stateService, adminService, projectRepo, questionRepo, menuHelper);
        var handler = new CommandHandler(
            userRepo, projectRepo, stateService,
            adminService, orderFlowService,
            config, uiRenderer, textInputHandler, menuHelper, adminCommandHandler);

        var botClient = Substitute.For<ITelegramBotClient>();
        var msg = TelegramFactory.CreateMessage(text: "📢 Рассылка");

        await handler.HandleCommandAsync(botClient, msg, CancellationToken.None);

        var msg2 = TelegramFactory.CreateMessage(text: "Hello everyone!");
        await handler.HandleCommandAsync(botClient, msg2, CancellationToken.None);

        var session = stateService.Get(1);
        Assert.That(session, Is.Not.Null);
        Assert.That(session!.TempBroadcastText, Is.EqualTo("Hello everyone!"));
        Assert.That(session.PendingAction, Is.Null);
    }
}
