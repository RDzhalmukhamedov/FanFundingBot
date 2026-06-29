using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.Tests.Bot.Handlers;

[TestFixture]
public class CallbackQueryHandlerTests
{
    private IUserStateService _stateService;
    private Mock<IAdminService> _adminServiceMock;
    private Mock<IUserCallbackQueryHandler> _userHandlerMock;
    private Mock<IAdminStatusCallbackHandler> _statusHandlerMock;
    private Mock<IAdminQuestionCallbackHandler> _questionHandlerMock;
    private Mock<IAdminProjectCallbackHandler> _projectHandlerMock;
    private CallbackQueryHandler _handler;
    private Mock<ITelegramBotClient> _botClientMock;

    [SetUp]
    public void Setup()
    {
        _stateService = new UserStateService(new MemoryCache(new MemoryCacheOptions()));
        _adminServiceMock = new Mock<IAdminService>();
        _adminServiceMock.Setup(s => s.IsAdminAsync(It.IsAny<long>())).ReturnsAsync(false);
        _userHandlerMock = new Mock<IUserCallbackQueryHandler>();
        _statusHandlerMock = new Mock<IAdminStatusCallbackHandler>();
        _questionHandlerMock = new Mock<IAdminQuestionCallbackHandler>();
        _projectHandlerMock = new Mock<IAdminProjectCallbackHandler>();

        // Create the callback command handlers
        var userCmdHandler = new UserCallbackCommandHandler(_userHandlerMock.Object);
        var adminCmdHandler = new AdminCallbackCommandHandler(
            _statusHandlerMock.Object, _questionHandlerMock.Object, _projectHandlerMock.Object);

        _handler = new CallbackQueryHandler(
            _adminServiceMock.Object, new ICallbackCommandHandler[] { userCmdHandler, adminCmdHandler });
        _botClientMock = new Mock<ITelegramBotClient>();
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
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleAddonToggleAsync(_botClientMock.Object, cq, 1, "addon_toggle:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AddonPage_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_page:2");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleAddonPageAsync(_botClientMock.Object, cq, 1, "addon_page:2", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AddonConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_confirm");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleAddonConfirmAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Delivery_RoutesToUserHandler()
    {
        var cq = MakeCq("delivery:pickup");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleDeliveryAsync(_botClientMock.Object, cq, 1, "delivery:pickup", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("order_confirm");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderConfirmAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderEdit_RoutesToUserHandler()
    {
        var cq = MakeCq("order_edit");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderEditAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderCancel_RoutesToUserHandler()
    {
        var cq = MakeCq("order_cancel");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderCancelAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Back_RoutesToUserHandler()
    {
        var cq = MakeCq("back:phone");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleBackAsync(_botClientMock.Object, cq, 1, "back:phone", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminStatus_AsAdmin_RoutesToStatusHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.Verify(h => h.HandleAdminStatusAsync(_botClientMock.Object, cq, 1, "admin_status:id:Completed", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminStatus_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AdminAnswer_AsAdmin_RoutesToQuestionHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_answer:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.Verify(h => h.HandleAdminSelectQuestionAsync(_botClientMock.Object, cq, 1, "admin_answer:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminAnswer_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_answer:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddonQtyInc_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_qty_inc:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleAddonQtyIncAsync(_botClientMock.Object, cq, 1, "addon_qty_inc:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AddonQtyDec_RoutesToUserHandler()
    {
        var cq = MakeCq("addon_qty_dec:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleAddonQtyDecAsync(_botClientMock.Object, cq, 1, "addon_qty_dec:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderEditMy_RoutesToUserHandler()
    {
        var cq = MakeCq("order_edit_my:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderEditMyAsync(_botClientMock.Object, cq, 1, "order_edit_my:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderDeleteMy_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderDeleteMyAsync(_botClientMock.Object, cq, 1, "order_delete_my:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderDeleteMyConfirm_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my_confirm:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderDeleteMyConfirmAsync(_botClientMock.Object, cq, 1, "order_delete_my_confirm:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task OrderDeleteMyCancel_RoutesToUserHandler()
    {
        var cq = MakeCq("order_delete_my_cancel:g");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.Verify(h => h.HandleOrderDeleteMyCancelAsync(_botClientMock.Object, cq, 1, "order_delete_my_cancel:g", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminAddStatusCanEdit_RoutesToProjectHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_add_status_can_edit:yes");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _projectHandlerMock.Verify(h => h.HandleAdminAddStatusCanEditAsync(_botClientMock.Object, cq, 1, "admin_add_status_can_edit:yes", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminAddStatusCanEdit_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_add_status_can_edit:yes");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _projectHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AdminCancel_RoutesToQuestionHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_cancel");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.Verify(h => h.HandleAdminCancelAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminCancel_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_cancel");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UnknownCommand_DoesNothing()
    {
        var cq = MakeCq("unknown:x");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _userHandlerMock.VerifyNoOtherCalls();
        _statusHandlerMock.VerifyNoOtherCalls();
        _questionHandlerMock.VerifyNoOtherCalls();
        _projectHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AdminBroadcastConfirm_AsAdmin_RoutesToQuestionHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_broadcast_confirm");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.Verify(h => h.HandleAdminBroadcastConfirmAsync(_botClientMock.Object, cq, 1, It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminBroadcastConfirm_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_broadcast_confirm");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _questionHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AdminStatusConfirm_AsAdmin_RoutesToStatusHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_status_confirm:id:5");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.Verify(h => h.HandleAdminStatusConfirmAsync(_botClientMock.Object, cq, 1, "admin_status_confirm:id:5", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminStatusConfirm_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_status_confirm:id:5");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AdminStatusCancel_AsAdmin_RoutesToStatusHandler()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        var cq = MakeCq("admin_status_cancel:id");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.Verify(h => h.HandleAdminStatusCancelAsync(_botClientMock.Object, cq, 1, "admin_status_cancel:id", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task AdminStatusCancel_AsNonAdmin_Rejects()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);
        var cq = MakeCq("admin_status_cancel:id");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
        _statusHandlerMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ExceptionInHandler_SendsError()
    {
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(true);
        _statusHandlerMock.Setup(h => h.HandleAdminStatusAsync(It.IsAny<ITelegramBotClient>(), It.IsAny<CallbackQuery>(), It.IsAny<long>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception());
        var cq = MakeCq("admin_status:id:Completed");
        await _handler.HandleCallbackQueryAsync(_botClientMock.Object, cq, CancellationToken.None);
    }
}