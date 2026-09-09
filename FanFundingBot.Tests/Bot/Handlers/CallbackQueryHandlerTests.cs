using System.Reflection;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Handlers;

[TestFixture]
public class CallbackQueryHandlerTests
{
    private IUserStateService _stateService;
    private IAuthorizationService _authorization;
    private IUserCallbackQueryHandler _userHandler;
    private IAdminStatusCallbackHandler _statusHandler;
    private IAdminQuestionCallbackHandler _questionHandler;
    private IAdminProjectCallbackHandler _projectHandler;
    private CallbackQueryHandler _handler;
    private ITelegramBotClient _botClient;

    [SetUp]
    public void Setup()
    {
        _stateService = new UserStateService(new MemoryCache(new MemoryCacheOptions()));
        _authorization = Substitute.For<IAuthorizationService>();
        _authorization.CanCreateProjectAsync(Arg.Any<long>()).Returns(false);
        _userHandler = Substitute.For<IUserCallbackQueryHandler>();
        _statusHandler = Substitute.For<IAdminStatusCallbackHandler>();
        _questionHandler = Substitute.For<IAdminQuestionCallbackHandler>();
        _projectHandler = Substitute.For<IAdminProjectCallbackHandler>();

        var userCmdHandler = new UserCallbackCommandHandler(_userHandler);
        var adminCmdHandler = new AdminCallbackCommandHandler(
            _statusHandler, _questionHandler, _projectHandler);

        _handler = new CallbackQueryHandler(
            _authorization, new TelegramUserContext(), NullLogger<CallbackQueryHandler>.Instance,
            new ICallbackCommandHandler[] { userCmdHandler, adminCmdHandler });
        _botClient = Substitute.For<ITelegramBotClient>();
    }

    private static CallbackQuery MakeCq(string data, long userId = 1)
    {
        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", data);
        SetProp(cq, "From", new User { Id = userId, FirstName = "T" });
        return cq;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    [Test]
    public async Task AddonToggle_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_toggle:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleAddonToggleAsync(_botClient, cq, 1, "addon_toggle:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonPage_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_page:2");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleAddonPageAsync(_botClient, cq, 1, "addon_page:2", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_confirm");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleAddonConfirmAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Delivery_RoutesToUserHandler()
    {
        var cq = MakeCq("delivery:pickup");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleDeliveryAsync(_botClient, cq, 1, "delivery:pickup", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("order_confirm");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderConfirmAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderEdit_RoutesToUserHandler()
    {
        var cq = MakeCq("order_edit");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderEditAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderCancel_RoutesToUserHandler()
    {
        var cq = MakeCq("order_cancel");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderCancelAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Back_RoutesToUserHandler()
    {
        var cq = MakeCq("back:phone");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleBackAsync(_botClient, cq, 1, "back:phone", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatus_AsAdmin_RoutesToStatusHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _statusHandler.Received(1).HandleAdminStatusAsync(_botClient, cq, 1, "admin_status:id:Completed", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatus_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _statusHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AdminAnswer_AsAdmin_RoutesToQuestionHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_answer:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _questionHandler.Received(1).HandleAdminSelectQuestionAsync(_botClient, cq, 1, "admin_answer:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminAnswer_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_answer:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _questionHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AddonQtyInc_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_qty_inc:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleAddonQtyIncAsync(_botClient, cq, 1, "addon_qty_inc:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonQtyDec_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_qty_dec:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleAddonQtyDecAsync(_botClient, cq, 1, "addon_qty_dec:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderEditMy_RoutesToUserHandler()
    {
        var cq = MakeCq("order_edit_my:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderEditMyAsync(_botClient, cq, 1, "order_edit_my:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderDeleteMy_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderDeleteMyAsync(_botClient, cq, 1, "order_delete_my:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderDeleteMyConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my_confirm:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderDeleteMyConfirmAsync(_botClient, cq, 1, "order_delete_my_confirm:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderDeleteMyCancel_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my_cancel:g");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _userHandler.Received(1).HandleOrderDeleteMyCancelAsync(_botClient, cq, 1, "order_delete_my_cancel:g", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminAddStatusCanEdit_RoutesToProjectHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_add_status_can_edit:yes");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _projectHandler.Received(1).HandleAdminAddStatusCanEditAsync(_botClient, cq, 1, "admin_add_status_can_edit:yes", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminAddStatusCanEdit_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_add_status_can_edit:yes");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _projectHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AdminCancel_RoutesToQuestionHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_cancel");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _questionHandler.Received(1).HandleAdminCancelAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminCancel_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_cancel");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _questionHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task UnknownCommand_DoesNothing()
    {
        var cq = MakeCq("unknown:x");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _userHandler.ReceivedCalls().Should().BeEmpty();
        _statusHandler.ReceivedCalls().Should().BeEmpty();
        _questionHandler.ReceivedCalls().Should().BeEmpty();
        _projectHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AdminBroadcastConfirm_AsAdmin_RoutesToQuestionHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_broadcast_confirm");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _questionHandler.Received(1).HandleAdminBroadcastConfirmAsync(_botClient, cq, 1, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminBroadcastConfirm_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_broadcast_confirm");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _questionHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AdminStatusConfirm_AsAdmin_RoutesToStatusHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_status_confirm:id:5");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _statusHandler.Received(1).HandleAdminStatusConfirmAsync(_botClient, cq, 1, "admin_status_confirm:id:5", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatusConfirm_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_status_confirm:id:5");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _statusHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task AdminStatusCancel_AsAdmin_RoutesToStatusHandler()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        var cq = MakeCq("admin_status_cancel:id");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        await _statusHandler.Received(1).HandleAdminStatusCancelAsync(_botClient, cq, 1, "admin_status_cancel:id", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AdminStatusCancel_AsNonAdmin_Rejects()
    {
        _authorization.CanCreateProjectAsync(1).Returns(false);
        var cq = MakeCq("admin_status_cancel:id");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
        _statusHandler.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task ExceptionInHandler_SendsError()
    {
        _authorization.CanCreateProjectAsync(1).Returns(true);
        _statusHandler.HandleAdminStatusAsync(Arg.Any<ITelegramBotClient>(), Arg.Any<CallbackQuery>(), Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception()));
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClient, cq, CancellationToken.None);
    }
}
