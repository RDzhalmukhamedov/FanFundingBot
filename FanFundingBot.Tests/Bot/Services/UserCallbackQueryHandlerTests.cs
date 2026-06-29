using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Exceptions;
using FanFundingBot.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class UserCallbackQueryHandlerTests
{
    private IMemoryCache _cache;
    private IUserStateService _stateService;
    private Mock<IOrderFlowService> _orderFlowServiceMock;
    private Mock<IProjectRepository> _projectRepoMock;
    private Mock<IOrderRepository> _orderRepoMock;
    private Mock<IUIRenderer> _uiRendererMock;
    private Mock<IAdminService> _adminServiceMock;
    private Mock<IOrderProcessingQueue> _processingQueueMock;
    private UserCallbackQueryHandler _handler;
    private Mock<ITelegramBotClient> _botClientMock;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AddonId1 = Guid.NewGuid();

    [TearDown]
    public void TearDown() => _cache?.Dispose();

    [SetUp]
    public void Setup()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _stateService = new UserStateService(_cache);

        _orderFlowServiceMock = new Mock<IOrderFlowService>();
        _uiRendererMock = new Mock<IUIRenderer>();
        _adminServiceMock = new Mock<IAdminService>();
        _orderRepoMock = new Mock<IOrderRepository>();

        _projectRepoMock = new Mock<IProjectRepository>();
        _processingQueueMock = new Mock<IOrderProcessingQueue>();
        var menuHelper = new MainMenuHelper(_adminServiceMock.Object);
        var projectAccess = new ProjectAccessHelper(_projectRepoMock.Object);
        _handler = new UserCallbackQueryHandler(
            _stateService, _orderFlowServiceMock.Object,
            _projectRepoMock.Object, _orderRepoMock.Object,
            _uiRendererMock.Object, _adminServiceMock.Object,
            _processingQueueMock.Object, menuHelper, projectAccess);
        _botClientMock = new Mock<ITelegramBotClient>();
    }

    private UserSession GetOrCreateSession(long userId = 1)
    {
        var s = _stateService.GetOrCreate(userId);
        s.State = OrderFlowState.SelectingAddons;
        s.SelectedProjectId = ProjectId;
        s.SelectedAddonIds = new HashSet<Guid>();
        s.AddonPage = 1;
        return s;
    }

    private static CallbackQuery MakeCq()
    {
        var msg = new Message();
        SetProp(msg, "MessageId", 100);
        SetProp(msg, "Chat", new Chat { Id = 200 });
        var cq = new CallbackQuery();
        SetProp(cq, "Id", "cq_id");
        SetProp(cq, "Data", "test");
        SetProp(cq, "Message", msg);
        SetProp(cq, "From", new Telegram.Bot.Types.User { Id = 1, FirstName = "Test" });
        return cq;
    }

    private static void SetProp<T>(T obj, string name, object val)
    {
        var p = typeof(T).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        p?.GetSetMethod(nonPublic: true)?.Invoke(obj, [val]);
    }

    // --- HandleAddonToggleAsync ---

    [Test]
    public async Task AddonToggle_WhenNotSelected_AddsAddonId()
    {
        var session = GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon>() });

        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.That(session.SelectedAddonIds, Contains.Item(AddonId1));
    }

    [Test]
    public async Task AddonToggle_WhenSelected_RemovesAddonId()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon>() });

        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.That(session.SelectedAddonIds, Does.Not.Contain(AddonId1));
    }

    [Test]
    public async Task AddonToggle_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 999, "addon_toggle:" + Guid.NewGuid(), CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddonToggle_NullProjectId_ReturnsEarly()
    {
        var session = GetOrCreateSession();
        session.SelectedProjectId = null;
        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);
        _projectRepoMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddonToggle_NullProject_ReturnsEarly()
    {
        GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId)).ReturnsAsync((Project?)null);
        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    // --- HandleAddonPageAsync ---

    [Test]
    public async Task AddonPage_ValidPage_UpdatesSession()
    {
        var session = GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId)).ReturnsAsync(new Project());
        await _handler.HandleAddonPageAsync(_botClientMock.Object, MakeCq(), 1, "addon_page:3", CancellationToken.None);
        Assert.That(session.AddonPage, Is.EqualTo(3));
    }

    [Test]
    public async Task AddonPage_NoPage_ReturnsEarly()
    {
        await _handler.HandleAddonPageAsync(_botClientMock.Object, MakeCq(), 1, "addon_page:nop", CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddonPage_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonPageAsync(_botClientMock.Object, MakeCq(), 1, "addon_page:2", CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    // --- HandleAddonConfirmAsync ---

    [Test]
    public async Task AddonConfirm_WithAddons_MovesToEnteringPhone()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        await _handler.HandleAddonConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.EnteringPhone));
    }

    [Test]
    public async Task AddonConfirm_NoAddons_ShowsAlert()
    {
        GetOrCreateSession();
        await _handler.HandleAddonConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
    }

    [Test]
    public async Task AddonConfirm_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        _botClientMock.VerifyNoOtherCalls();
    }

    // --- HandleDeliveryAsync ---

    [Test]
    public async Task Delivery_Pickup_SetsPickupAndConfirms()
    {
        var session = GetOrCreateSession();
        await _handler.HandleDeliveryAsync(_botClientMock.Object, MakeCq(), 1, "delivery:pickup", CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(session.DeliveryMethod, Is.EqualTo(DeliveryMethod.Pickup));
            Assert.That(session.State, Is.EqualTo(OrderFlowState.Confirming));
        });
    }

    [Test]
    public async Task Delivery_Delivery_SetsDeliveryAndAsksAddress()
    {
        var session = GetOrCreateSession();
        await _handler.HandleDeliveryAsync(_botClientMock.Object, MakeCq(), 1, "delivery:delivery", CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(session.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(session.State, Is.EqualTo(OrderFlowState.EnteringAddress));
        });
    }

    [Test]
    public async Task Delivery_NullSession_ReturnsEarly()
    {
        await _handler.HandleDeliveryAsync(_botClientMock.Object, MakeCq(), 1, "delivery:x", CancellationToken.None);
        _botClientMock.VerifyNoOtherCalls();
    }

    // --- HandleOrderConfirmAsync ---

    [Test]
    public async Task OrderConfirm_CreatesOrderAndRemovesSession()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowServiceMock.Setup(s => s.CreateOrderAsync(1, ProjectId, session.SelectedAddonIds, It.IsAny<Dictionary<Guid, int>>(), "+7", DeliveryMethod.Pickup, null))
            .ReturnsAsync(new Order { Id = Guid.NewGuid() });
        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_OrderSaveException_SessionRemoved()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowServiceMock.Setup(s => s.CreateOrderAsync(It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<HashSet<Guid>>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<string>(), It.IsAny<DeliveryMethod>(), It.IsAny<string?>()))
            .ThrowsAsync(new OrderSaveException("fail"));
        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_GenericException_SessionRemoved()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowServiceMock.Setup(s => s.CreateOrderAsync(It.IsAny<long>(), It.IsAny<Guid>(), It.IsAny<HashSet<Guid>>(), It.IsAny<Dictionary<Guid, int>>(), It.IsAny<string>(), It.IsAny<DeliveryMethod>(), It.IsAny<string?>()))
            .ThrowsAsync(new Exception());
        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_NullSession_ReturnsEarly()
    {
        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        _orderFlowServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task OrderConfirm_MissingFields_ReturnsEarly()
    {
        var session = GetOrCreateSession();
        session.Phone = null;
        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        _orderFlowServiceMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task OrderConfirm_EnqueuesToProcessingQueue()
    {
        var session = GetOrCreateSession();
        session.Phone = "+71234567890";
        session.DeliveryMethod = DeliveryMethod.Pickup;

        await _handler.HandleOrderConfirmAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);

        _processingQueueMock.Verify(q => q.EnqueueAsync(
            It.Is<OrderSaveRequest>(r => r.UserId == 1 && r.Phone == "+71234567890" && r.IsEditing == false),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // --- HandleAddonQtyIncAsync / HandleAddonQtyDecAsync ---

    [Test]
    public async Task AddonQtyInc_IncreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 2;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon> { new() { Id = AddonId1, Name = "A1", Price = 100 } } });

        await _handler.HandleAddonQtyIncAsync(_botClientMock.Object, MakeCq(), 1, "addon_qty_inc:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(3));
    }

    [Test]
    public async Task AddonQtyDec_DecreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 3;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon> { new() { Id = AddonId1, Name = "A1", Price = 100 } } });

        await _handler.HandleAddonQtyDecAsync(_botClientMock.Object, MakeCq(), 1, "addon_qty_dec:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(2));
    }

    [Test]
    public async Task AddonQtyDec_AtMinimum_DoesNotDecrease()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 1;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon> { new() { Id = AddonId1, Name = "A1", Price = 100 } } });

        await _handler.HandleAddonQtyDecAsync(_botClientMock.Object, MakeCq(), 1, "addon_qty_dec:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(1));
    }

    [Test]
    public async Task AddonQtyInc_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonQtyIncAsync(_botClientMock.Object, MakeCq(), 999, "addon_qty_inc:" + Guid.NewGuid(), CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task AddonQtyDec_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonQtyDecAsync(_botClientMock.Object, MakeCq(), 999, "addon_qty_dec:" + Guid.NewGuid(), CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    // --- HandleAddonToggleAsync (quantity tracking) ---

    [Test]
    public async Task AddonToggle_WhenNotSelected_SetsQuantityToOne()
    {
        var session = GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon>() });

        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(session.SelectedAddonIds, Contains.Item(AddonId1));
            Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AddonToggle_WhenSelected_RemovesQuantity()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        session.AddonQuantities[AddonId1] = 5;
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId))
            .ReturnsAsync(new Project { Id = ProjectId, Addons = new List<Addon>() });

        await _handler.HandleAddonToggleAsync(_botClientMock.Object, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(session.SelectedAddonIds, Does.Not.Contain(AddonId1));
            Assert.That(session.AddonQuantities.ContainsKey(AddonId1), Is.False);
        });
    }

    // --- HandleOrderEditMyAsync ---

    [Test]
    public async Task OrderEditMy_NonExistentOrder_ShowsAlert()
    {
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync((Order?)null);

        await _handler.HandleOrderEditMyAsync(_botClientMock.Object, MakeCq(), 1, "order_edit_my:" + Guid.NewGuid(), CancellationToken.None);
        // No exception thrown = alert was shown
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditFalse_ShowsAlert()
    {
        var orderId = Guid.NewGuid();
        var order = new Order { Id = orderId, ProjectId = ProjectId, OrderAddons = new List<OrderAddon>() };
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(order);

        var project = new Project { Id = ProjectId, Status = new ProjectStatusEntry { CanEdit = false } };
        _projectRepoMock.Setup(r => r.GetByIdAsync(ProjectId)).ReturnsAsync(project);

        await _handler.HandleOrderEditMyAsync(_botClientMock.Object, MakeCq(), 1, "order_edit_my:" + orderId, CancellationToken.None);

        // No exception thrown = alert was shown
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditTrue_StartsEditing()
    {
        var orderId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            ProjectId = ProjectId,
            Phone = "+79991234567",
            DeliveryMethod = DeliveryMethod.Delivery,
            Address = "Test Address",
            OrderAddons = new List<OrderAddon>
            {
                new() { AddonId = addonId, Quantity = 3 }
            }
        };
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(order);

        var project = new Project { Id = ProjectId, Addons = new List<Addon>(), Status = new ProjectStatusEntry { CanEdit = true } };
        _projectRepoMock.Setup(r => r.GetByIdAsync(ProjectId)).ReturnsAsync(project);

        await _handler.HandleOrderEditMyAsync(_botClientMock.Object, MakeCq(), 1, "order_edit_my:" + orderId, CancellationToken.None);

        var session = _stateService.Get(1);
        Assert.Multiple(() =>
        {
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.EditingOrderId, Is.EqualTo(orderId));
            Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
            Assert.That(session.Phone, Is.EqualTo("+79991234567"));
            Assert.That(session.AddonQuantities[addonId], Is.EqualTo(3));
        });
    }

    // --- HandleOrderDeleteMyAsync / Confirm / Cancel ---

    [Test]
    public async Task OrderDeleteMy_ShowsConfirmation()
    {
        var orderId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(new Order { ProjectId = ProjectId });
        _projectRepoMock.Setup(r => r.GetByIdAsync(ProjectId)).ReturnsAsync(new Project { Status = new ProjectStatusEntry { CanEdit = true } });
        await _handler.HandleOrderDeleteMyAsync(_botClientMock.Object, MakeCq(), 1, "order_delete_my:" + orderId, CancellationToken.None);
        // Verify no exception was thrown - keyboard with confirmation was shown
    }

    [Test]
    public async Task OrderDeleteMyConfirm_DeletesOrder()
    {
        var orderId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(new Order { ProjectId = ProjectId });
        _projectRepoMock.Setup(r => r.GetByIdAsync(ProjectId)).ReturnsAsync(new Project { Status = new ProjectStatusEntry { CanEdit = true } });
        _adminServiceMock.Setup(s => s.IsAdminAsync(1)).ReturnsAsync(false);

        await _handler.HandleOrderDeleteMyConfirmAsync(_botClientMock.Object, MakeCq(), 1, "order_delete_my_confirm:" + orderId, CancellationToken.None);

        _orderFlowServiceMock.Verify(s => s.DeleteOrderAsync(orderId), Times.Once);
    }

    [Test]
    public async Task OrderDeleteMyCancel_ShowsCancelled()
    {
        var orderId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync((Order?)null);

        await _handler.HandleOrderDeleteMyCancelAsync(_botClientMock.Object, MakeCq(), 1, "order_delete_my_cancel:" + orderId, CancellationToken.None);

        _orderFlowServiceMock.VerifyNoOtherCalls();
    }

    // --- HandleOrderEditAsync ---

    [Test]
    public async Task OrderEdit_GoesBackToAddonSelection()
    {
        var session = GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId)).ReturnsAsync(new Project());
        await _handler.HandleOrderEditAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
    }

    [Test]
    public async Task OrderEdit_NullSession_ReturnsEarly()
    {
        await _handler.HandleOrderEditAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }

    // --- HandleOrderCancelAsync ---

    [Test]
    public async Task OrderCancel_RemovesSession()
    {
        GetOrCreateSession();
        await _handler.HandleOrderCancelAsync(_botClientMock.Object, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    // --- HandleBackAsync ---

    [Test]
    public async Task Back_Phone_GoesToAddonSelection()
    {
        var session = GetOrCreateSession();
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(ProjectId)).ReturnsAsync(new Project());
        await _handler.HandleBackAsync(_botClientMock.Object, MakeCq(), 1, "back:phone", CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
    }

    [Test]
    public async Task Back_Delivery_GoesToDeliverySelection()
    {
        var session = GetOrCreateSession();
        session.State = OrderFlowState.EnteringAddress;
        await _handler.HandleBackAsync(_botClientMock.Object, MakeCq(), 1, "back:delivery", CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingDelivery));
    }

    [Test]
    public async Task Back_NullSession_ReturnsEarly()
    {
        await _handler.HandleBackAsync(_botClientMock.Object, MakeCq(), 1, "back:x", CancellationToken.None);
        _uiRendererMock.VerifyNoOtherCalls();
    }
}
