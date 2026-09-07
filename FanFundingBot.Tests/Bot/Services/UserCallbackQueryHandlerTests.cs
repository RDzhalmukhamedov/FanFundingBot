using System.Reflection;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
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
    private IOrderFlowService _orderFlowService;
    private IProjectRepository _projectRepo;
    private IOrderRepository _orderRepo;
    private IUIRenderer _uiRenderer;
    private IAdminService _adminService;
    private IOrderProcessingQueue _processingQueue;
    private UserCallbackQueryHandler _handler;
    private ITelegramBotClient _botClient;

    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid AddonId1 = Guid.NewGuid();

    [TearDown]
    public void TearDown() => _cache?.Dispose();

    [SetUp]
    public void Setup()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());
        _stateService = new UserStateService(_cache);

        _orderFlowService = Substitute.For<IOrderFlowService>();
        _uiRenderer = Substitute.For<IUIRenderer>();
        _adminService = Substitute.For<IAdminService>();
        _orderRepo = Substitute.For<IOrderRepository>();

        _projectRepo = Substitute.For<IProjectRepository>();
        _processingQueue = Substitute.For<IOrderProcessingQueue>();
        var menuHelper = new MainMenuHelper(_adminService);
        var projectAccess = new ProjectAccessHelper(_projectRepo);
        _handler = new UserCallbackQueryHandler(
            _stateService, _orderFlowService,
            _projectRepo, _orderRepo,
            _uiRenderer, _adminService,
            _processingQueue, menuHelper, projectAccess);
        _botClient = Substitute.For<ITelegramBotClient>();
    }

    private UserSession GetOrCreateSession(long userId = 1)
    {
        var s = _stateService.GetOrCreate(userId);
        s.State = OrderFlowState.SelectingAddons;
        s.SelectedProjectId = ProjectId;
        s.SelectedAddonIds = new HashSet<Guid>();
        s.AddonPage = 1;
        s.EditMessageId = 1;
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

    private static Project ProjectWithAddon()
    {
        var project = DomainTestFactory.NewProject();
        project.AddAddon(DomainTestFactory.NewAddon(AddonId1, "A1", 100));
        return project;
    }

    [Test]
    public async Task AddonToggle_WhenNotSelected_AddsAddonId()
    {
        var session = GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(DomainTestFactory.NewProject());

        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.That(session.SelectedAddonIds, Contains.Item(AddonId1));
    }

    [Test]
    public async Task AddonToggle_WhenSelected_RemovesAddonId()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(DomainTestFactory.NewProject());

        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.That(session.SelectedAddonIds, Does.Not.Contain(AddonId1));
    }

    [Test]
    public async Task AddonToggle_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 999, "addon_toggle:" + Guid.NewGuid(), CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AddonToggle_NullProjectId_ReturnsEarly()
    {
        var session = GetOrCreateSession();
        session.SelectedProjectId = null;
        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);
        await _projectRepo.DidNotReceive().GetByIdWithAddonsAsync(Arg.Any<Guid>());
    }

    [Test]
    public async Task AddonToggle_NullProject_ReturnsEarly()
    {
        GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId).Returns((Project?)null);
        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AddonPage_ValidPage_UpdatesSession()
    {
        var session = GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId).Returns(new Project());
        await _handler.HandleAddonPageAsync(_botClient, MakeCq(), 1, "addon_page:3", CancellationToken.None);
        Assert.That(session.AddonPage, Is.EqualTo(3));
    }

    [Test]
    public async Task AddonPage_NoPage_ReturnsEarly()
    {
        await _handler.HandleAddonPageAsync(_botClient, MakeCq(), 1, "addon_page:nop", CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AddonPage_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonPageAsync(_botClient, MakeCq(), 1, "addon_page:2", CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AddonConfirm_WithAddons_MovesToEnteringPhone()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        await _handler.HandleAddonConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.EnteringPhone));
    }

    [Test]
    public async Task AddonConfirm_NoAddons_ShowsAlert()
    {
        GetOrCreateSession();
        await _handler.HandleAddonConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
    }

    [Test]
    public async Task AddonConfirm_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        _botClient.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task Delivery_Pickup_SetsPickupAndConfirms()
    {
        var session = GetOrCreateSession();
        await _handler.HandleDeliveryAsync(_botClient, MakeCq(), 1, "delivery:pickup", CancellationToken.None);
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
        await _handler.HandleDeliveryAsync(_botClient, MakeCq(), 1, "delivery:delivery", CancellationToken.None);
        Assert.Multiple(() =>
        {
            Assert.That(session.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(session.State, Is.EqualTo(OrderFlowState.EnteringAddress));
        });
    }

    [Test]
    public async Task Delivery_NullSession_ReturnsEarly()
    {
        await _handler.HandleDeliveryAsync(_botClient, MakeCq(), 1, "delivery:x", CancellationToken.None);
        _botClient.ReceivedCalls().Should().BeEmpty();
    }

    [Test]
    public async Task OrderConfirm_CreatesOrderAndRemovesSession()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowService.CreateOrderAsync(1, ProjectId, session.SelectedAddonIds, Arg.Any<Dictionary<Guid, int>>(), "+7", DeliveryMethod.Pickup, null)
            .Returns(DomainTestFactory.NewFormed());
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_OrderSaveException_SessionRemoved()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowService.CreateOrderAsync(Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<HashSet<Guid>>(), Arg.Any<Dictionary<Guid, int>>(), Arg.Any<string>(), Arg.Any<DeliveryMethod>(), Arg.Any<string?>())
            .Returns<Task<Order>>(x => throw new OrderSaveException("fail"));
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_GenericException_SessionRemoved()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _orderFlowService.CreateOrderAsync(Arg.Any<long>(), Arg.Any<Guid>(), Arg.Any<HashSet<Guid>>(), Arg.Any<Dictionary<Guid, int>>(), Arg.Any<string>(), Arg.Any<DeliveryMethod>(), Arg.Any<string?>())
            .Returns<Task<Order>>(x => throw new Exception());
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderConfirm_NullSession_ReturnsEarly()
    {
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        await _orderFlowService.DidNotReceiveWithAnyArgs().CreateOrderAsync(default, default, default, default, default, default, default);
    }

    [Test]
    public async Task OrderConfirm_MissingFields_ReturnsEarly()
    {
        var session = GetOrCreateSession();
        session.Phone = null;
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        await _orderFlowService.DidNotReceiveWithAnyArgs().CreateOrderAsync(default, default, default, default, default, default, default);
    }

    [Test]
    public async Task OrderConfirm_EnqueuesToProcessingQueue()
    {
        var session = GetOrCreateSession();
        session.Phone = "+71234567890";
        session.DeliveryMethod = DeliveryMethod.Pickup;

        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);

        await _processingQueue.Received(1).EnqueueAsync(
            Arg.Is<OrderSaveRequest>(r => r.UserId == 1 && r.Phone == "+71234567890" && r.IsEditing == false),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonQtyInc_IncreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 2;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(ProjectWithAddon());

        await _handler.HandleAddonQtyIncAsync(_botClient, MakeCq(), 1, "addon_qty_inc:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(3));
    }

    [Test]
    public async Task AddonQtyDec_DecreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 3;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(ProjectWithAddon());

        await _handler.HandleAddonQtyDecAsync(_botClient, MakeCq(), 1, "addon_qty_dec:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(2));
    }

    [Test]
    public async Task AddonQtyDec_AtMinimum_DoesNotDecrease()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 1;
        session.SelectedAddonIds.Add(AddonId1);
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(ProjectWithAddon());

        await _handler.HandleAddonQtyDecAsync(_botClient, MakeCq(), 1, "addon_qty_dec:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(1));
    }

    [Test]
    public async Task AddonQtyInc_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonQtyIncAsync(_botClient, MakeCq(), 999, "addon_qty_inc:" + Guid.NewGuid(), CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().ShowConfirmationAsync(default, default, default, default);
    }

    [Test]
    public async Task AddonQtyDec_NullSession_ReturnsEarly()
    {
        await _handler.HandleAddonQtyDecAsync(_botClient, MakeCq(), 999, "addon_qty_dec:" + Guid.NewGuid(), CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().ShowConfirmationAsync(default, default, default, default);
    }

    [Test]
    public async Task AddonToggle_WhenNotSelected_SetsQuantityToOne()
    {
        var session = GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(DomainTestFactory.NewProject());

        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

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
        _projectRepo.GetByIdWithAddonsAsync(ProjectId)
            .Returns(DomainTestFactory.NewProject());

        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(session.SelectedAddonIds, Does.Not.Contain(AddonId1));
            Assert.That(session.AddonQuantities.ContainsKey(AddonId1), Is.False);
        });
    }

    [Test]
    public async Task OrderEditMy_NonExistentOrder_ShowsAlert()
    {
        _orderRepo.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + Guid.NewGuid(), CancellationToken.None);
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditFalse_ShowsAlert()
    {
        var orderId = Guid.NewGuid();
        var order = DomainTestFactory.NewFormed();
        _orderRepo.GetByIdWithAddonsAsync(orderId).Returns(order);

        var project = DomainTestFactory.NewProject(canEditOrders: false);
        _projectRepo.GetByIdAsync(Arg.Any<Guid>()).Returns(project);

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + orderId, CancellationToken.None);
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditTrue_StartsEditing()
    {
        var orderId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var customer = DomainTestFactory.NewCustomer();
        var orderProject = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(customer, orderProject, addonId, 3, "+79991234567", DeliveryMethod.Delivery, "Test Address");
        _orderRepo.GetByIdWithAddonsAsync(orderId).Returns(order);

        var project = DomainTestFactory.NewProject();
        _projectRepo.GetByIdAsync(Arg.Any<Guid>()).Returns(project);
        _stateService.GetOrCreate(1).EditMessageId = 1;

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + orderId, CancellationToken.None);

        var session = _stateService.Get(1);
        Assert.Multiple(() =>
        {
            Assert.That(session, Is.Not.Null);
            Assert.That(session!.EditingOrderId, Is.EqualTo(order.Id));
            Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
            Assert.That(session.Phone, Is.EqualTo("+79991234567"));
            Assert.That(session.AddonQuantities[addonId], Is.EqualTo(3));
        });

        _stateService.Remove(1);
    }

    [Test]
    public async Task OrderDeleteMy_ShowsConfirmation()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.GetByIdWithAddonsAsync(orderId).Returns(DomainTestFactory.NewFormed());
        _projectRepo.GetByIdAsync(Arg.Any<Guid>()).Returns(DomainTestFactory.NewProject());

        await _handler.HandleOrderDeleteMyAsync(_botClient, MakeCq(), 1, "order_delete_my:" + orderId, CancellationToken.None);
    }

    [Test]
    public async Task OrderDeleteMyConfirm_DeletesOrder()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.GetByIdWithAddonsAsync(orderId).Returns(DomainTestFactory.NewFormed());
        _projectRepo.GetByIdAsync(Arg.Any<Guid>()).Returns(DomainTestFactory.NewProject());
        _adminService.IsAdminAsync(1).Returns(false);

        await _handler.HandleOrderDeleteMyConfirmAsync(_botClient, MakeCq(), 1, "order_delete_my_confirm:" + orderId, CancellationToken.None);

        await _orderFlowService.Received(1).DeleteOrderAsync(orderId);
    }

    [Test]
    public async Task OrderDeleteMyCancel_ShowsCancelled()
    {
        var orderId = Guid.NewGuid();
        _orderRepo.GetByIdWithAddonsAsync(orderId).Returns((Order?)null);

        await _handler.HandleOrderDeleteMyCancelAsync(_botClient, MakeCq(), 1, "order_delete_my_cancel:" + orderId, CancellationToken.None);

        await _orderFlowService.DidNotReceiveWithAnyArgs().DeleteOrderAsync(default);
    }

    [Test]
    public async Task OrderEdit_GoesBackToAddonSelection()
    {
        var session = GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId).Returns(new Project());
        await _handler.HandleOrderEditAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
    }

    [Test]
    public async Task OrderEdit_NullSession_ReturnsEarly()
    {
        await _handler.HandleOrderEditAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task OrderCancel_RemovesSession()
    {
        GetOrCreateSession();
        await _handler.HandleOrderCancelAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task Back_Phone_GoesToAddonSelection()
    {
        var session = GetOrCreateSession();
        _projectRepo.GetByIdWithAddonsAsync(ProjectId).Returns(new Project());
        await _handler.HandleBackAsync(_botClient, MakeCq(), 1, "back:phone", CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingAddons));
    }

    [Test]
    public async Task Back_Delivery_GoesToDeliverySelection()
    {
        var session = GetOrCreateSession();
        session.State = OrderFlowState.EnteringAddress;
        await _handler.HandleBackAsync(_botClient, MakeCq(), 1, "back:delivery", CancellationToken.None);
        Assert.That(session.State, Is.EqualTo(OrderFlowState.SelectingDelivery));
    }

    [Test]
    public async Task Back_NullSession_ReturnsEarly()
    {
        await _handler.HandleBackAsync(_botClient, MakeCq(), 1, "back:x", CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }
}
