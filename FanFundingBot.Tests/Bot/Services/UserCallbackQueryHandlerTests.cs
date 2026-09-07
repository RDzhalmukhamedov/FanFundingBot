using System.Reflection;
using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.DTOs;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Application.Projects.DTOs;
using FanFundingBot.Application.Projects.Queries;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Helpers;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FluentAssertions;
using FluentResults;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using ApplicationAbstractions = FanFundingBot.Application.Abstractions;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class UserCallbackQueryHandlerTests
{
    private IMemoryCache _cache;
    private IUserStateService _stateService;
    private IMediator _mediator;
    private IUIRenderer _uiRenderer;
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

        _mediator = Substitute.For<IMediator>();
        _uiRenderer = Substitute.For<IUIRenderer>();
        var authorization = Substitute.For<ApplicationAbstractions.IAuthorizationService>();
        var menuHelper = new MainMenuHelper(authorization);
        _handler = new UserCallbackQueryHandler(
            _stateService, _mediator, _uiRenderer, menuHelper);
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

    private static ProjectDto ProjectWithAddon()
    {
        return new ProjectDto(
            ProjectId, "P", null, 1, "S", true, null,
            new List<AddonDto>
            {
                new(AddonId1, "A1", null, 100m)
            },
            new List<ProjectMemberDto>());
    }

    private void SetupProjectQuery(ProjectDto? project = null)
    {
        _mediator.Send(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(project is null
                ? Result.Fail<ProjectDto>("Проект не найден.")
                : Result.Ok(project));
    }

    [Test]
    public async Task AddonToggle_WhenNotSelected_AddsAddonId()
    {
        var session = GetOrCreateSession();
        SetupProjectQuery(TestProjectDto(true));

        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);

        Assert.That(session.SelectedAddonIds, Contains.Item(AddonId1));
    }

    [Test]
    public async Task AddonToggle_WhenSelected_RemovesAddonId()
    {
        var session = GetOrCreateSession();
        session.SelectedAddonIds.Add(AddonId1);
        SetupProjectQuery(TestProjectDto(true));

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
        await _mediator.DidNotReceive().Send(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonToggle_NullProject_ReturnsEarly()
    {
        GetOrCreateSession();
        SetupProjectQuery(null);
        await _handler.HandleAddonToggleAsync(_botClient, MakeCq(), 1, "addon_toggle:" + AddonId1, CancellationToken.None);
        await _uiRenderer.DidNotReceiveWithAnyArgs().SendAddonSelectionAsync(default, default, default, default, default);
    }

    [Test]
    public async Task AddonPage_ValidPage_UpdatesSession()
    {
        var session = GetOrCreateSession();
        SetupProjectQuery(TestProjectDto(true));
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
    public async Task OrderConfirm_NewOrder_SendsCreateCommand()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        var orderId = Guid.NewGuid();
        _mediator.Send(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(orderId));

        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);

        Assert.That(_stateService.Get(1), Is.Null);
        await _mediator.Received(1).Send(
            Arg.Is<CreateOrderCommand>(c => c.CallerUserId == 1 && c.ProjectId == ProjectId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderConfirm_FailedCreate_KeepsSession()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        _mediator.Send(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<Guid>("DB down"));

        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);

        Assert.That(_stateService.Get(1), Is.Not.Null);
    }

    [Test]
    public async Task OrderConfirm_EditingOrder_SendsUpdateCommand()
    {
        var session = GetOrCreateSession();
        session.Phone = "+7";
        session.DeliveryMethod = DeliveryMethod.Pickup;
        session.EditingOrderId = Guid.NewGuid();
        _mediator.Send(Arg.Any<UpdateOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);

        Assert.That(_stateService.Get(1), Is.Null);
        await _mediator.Received(1).Send(
            Arg.Is<UpdateOrderCommand>(c => c.OrderId == session.EditingOrderId),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderConfirm_NullSession_ReturnsEarly()
    {
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        await _mediator.DidNotReceive().Send(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderConfirm_MissingFields_ReturnsEarly()
    {
        var session = GetOrCreateSession();
        session.Phone = null;
        await _handler.HandleOrderConfirmAsync(_botClient, MakeCq(), 1, CancellationToken.None);
        await _mediator.DidNotReceive().Send(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task AddonQtyInc_IncreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 2;
        session.SelectedAddonIds.Add(AddonId1);
        SetupProjectQuery(ProjectWithAddon());

        await _handler.HandleAddonQtyIncAsync(_botClient, MakeCq(), 1, "addon_qty_inc:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(3));
    }

    [Test]
    public async Task AddonQtyDec_DecreasesQuantity()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 3;
        session.SelectedAddonIds.Add(AddonId1);
        SetupProjectQuery(ProjectWithAddon());

        await _handler.HandleAddonQtyDecAsync(_botClient, MakeCq(), 1, "addon_qty_dec:" + AddonId1, CancellationToken.None);

        Assert.That(session.AddonQuantities[AddonId1], Is.EqualTo(2));
    }

    [Test]
    public async Task AddonQtyDec_AtMinimum_DoesNotDecrease()
    {
        var session = GetOrCreateSession();
        session.AddonQuantities[AddonId1] = 1;
        session.SelectedAddonIds.Add(AddonId1);
        SetupProjectQuery(ProjectWithAddon());

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
        SetupProjectQuery(TestProjectDto(true));

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
        SetupProjectQuery(TestProjectDto(true));

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
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OrderDto>("Заказ не найден."));

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + Guid.NewGuid(), CancellationToken.None);
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditFalse_ShowsAlert()
    {
        var order = TestOrderDto(canEdit: false);
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: false));

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + order.Id, CancellationToken.None);

        Assert.That(_stateService.Get(1), Is.Null);
    }

    [Test]
    public async Task OrderEditMy_WhenCanEditTrue_StartsEditing()
    {
        var addonId = Guid.NewGuid();
        var order = TestOrderDto(addonId, 3);
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: true));
        _stateService.GetOrCreate(1).EditMessageId = 1;

        await _handler.HandleOrderEditMyAsync(_botClient, MakeCq(), 1, "order_edit_my:" + order.Id, CancellationToken.None);

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
        var order = TestOrderDto();
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: true));

        await _handler.HandleOrderDeleteMyAsync(_botClient, MakeCq(), 1, "order_delete_my:" + order.Id, CancellationToken.None);
    }

    [Test]
    public async Task OrderDeleteMy_NotEditable_ShowsAlert()
    {
        var order = TestOrderDto();
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: false));

        await _handler.HandleOrderDeleteMyAsync(_botClient, MakeCq(), 1, "order_delete_my:" + order.Id, CancellationToken.None);
    }

    [Test]
    public async Task OrderDeleteMyConfirm_DeletesOrder()
    {
        var order = TestOrderDto();
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: true));
        _mediator.Send(Arg.Any<DeleteOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok());

        await _handler.HandleOrderDeleteMyConfirmAsync(_botClient, MakeCq(), 1, "order_delete_my_confirm:" + order.Id, CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<DeleteOrderCommand>(c => c.OrderId == order.Id),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderDeleteMyConfirm_FailedDelete_ShowsAlert()
    {
        var order = TestOrderDto();
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Ok(order));
        SetupProjectQuery(TestProjectDto(canEdit: true));
        _mediator.Send(Arg.Any<DeleteOrderCommand>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail("Нет прав."));

        await _handler.HandleOrderDeleteMyConfirmAsync(_botClient, MakeCq(), 1, "order_delete_my_confirm:" + order.Id, CancellationToken.None);
    }

    [Test]
    public async Task OrderDeleteMyCancel_ShowsCancelled()
    {
        var orderId = Guid.NewGuid();
        _mediator.Send(Arg.Any<GetOrderByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(Result.Fail<OrderDto>("Заказ не найден."));

        await _handler.HandleOrderDeleteMyCancelAsync(_botClient, MakeCq(), 1, "order_delete_my_cancel:" + orderId, CancellationToken.None);
    }

    [Test]
    public async Task OrderEdit_GoesBackToAddonSelection()
    {
        var session = GetOrCreateSession();
        SetupProjectQuery(TestProjectDto(true));
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
        SetupProjectQuery(TestProjectDto(true));
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

    private static OrderDto TestOrderDto(Guid? addonId = null, int quantity = 1, bool canEdit = true)
    {
        var aid = addonId ?? Guid.NewGuid();
        return new OrderDto(
            Guid.NewGuid(), 1, ProjectId, "P", "+79991234567",
            DeliveryMethod.Delivery, "Test Address", OrderStatus.New, DateTime.UtcNow,
            new List<OrderPositionDto>
            {
                new(aid, "A", 10m, quantity)
            },
            10m * quantity);
    }

    private static ProjectDto TestProjectDto(bool canEdit)
    {
        return new ProjectDto(
            ProjectId, "P", null, 1, "S", canEdit, null,
            new List<AddonDto>(),
            new List<ProjectMemberDto>());
    }
}
