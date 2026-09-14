using FanFundingBot.Application.Abstractions;
using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace FanFundingBot.Tests.Application.Orders;

[TestFixture]
public class UpdateOrderStatusCommandTests
{
    private IOrderRepository _orders;
    private IAuthorizationService _auth;
    private INotificationPort _notifications;
    private UpdateOrderStatusCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _orders = Substitute.For<IOrderRepository>();
        _auth = Substitute.For<IAuthorizationService>();
        _notifications = Substitute.For<INotificationPort>();
        _handler = new UpdateOrderStatusCommandHandler(_orders, _auth, _notifications);
    }

    [Test]
    public async Task Happy_ShouldUpdateNotifyAndRaiseEvent()
    {
        var customer = DomainTestFactory.NewCustomer(7);
        var project = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(customer, project);
        order.ClearDomainEvents();
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);
        _auth.CanManageProjectAsync(1, project.Id).Returns(true);

        var result = await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, 1, OrderStatus.Confirmed), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
        await _orders.Received(1).UpdateAsync(order);
        await _notifications.Received(1).NotifyOrderStatusChangedAsync(
            7, project.Name, OrderStatus.Confirmed, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NotFound_ShouldFail()
    {
        _orders.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        var result = await _handler.Handle(
            new UpdateOrderStatusCommand(Guid.NewGuid(), 1, OrderStatus.Confirmed), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _notifications.DidNotReceive().NotifyOrderStatusChangedAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NoRights_ShouldFail()
    {
        var order = DomainTestFactory.NewFormed();
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);
        _auth.CanManageProjectAsync(999, order.Project.Id).Returns(false);

        var result = await _handler.Handle(
            new UpdateOrderStatusCommand(order.Id, 999, OrderStatus.Confirmed), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _orders.DidNotReceive().UpdateAsync(Arg.Any<Order>());
        await _notifications.DidNotReceive().NotifyOrderStatusChangedAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<OrderStatus>(), Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class SendDirectMessageCommandTests
{
    private IOrderRepository _orders;
    private IAuthorizationService _auth;
    private INotificationPort _notifications;
    private SendDirectMessageCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _orders = Substitute.For<IOrderRepository>();
        _auth = Substitute.For<IAuthorizationService>();
        _notifications = Substitute.For<INotificationPort>();
        _handler = new SendDirectMessageCommandHandler(_orders, _auth, _notifications);
    }

    [Test]
    public async Task Happy_ShouldSendToOrderOwner()
    {
        var customer = DomainTestFactory.NewCustomer(7);
        var order = DomainTestFactory.NewFormed(customer, DomainTestFactory.NewProject());
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);
        _auth.CanManageProjectAsync(1, order.Project.Id).Returns(true);

        var result = await _handler.Handle(
            new SendDirectMessageCommand(1, order.Id, "Hello"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _notifications.Received(1).SendDirectMessageAsync(
            7, "Hello", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderNotFound_ShouldFail()
    {
        _orders.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        var result = await _handler.Handle(
            new SendDirectMessageCommand(1, Guid.NewGuid(), "Hello"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _notifications.DidNotReceive().SendDirectMessageAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task NoRights_ShouldFail()
    {
        var order = DomainTestFactory.NewFormed();
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);
        _auth.CanManageProjectAsync(999, order.Project.Id).Returns(false);

        var result = await _handler.Handle(
            new SendDirectMessageCommand(999, order.Id, "Hello"), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _notifications.DidNotReceive().SendDirectMessageAsync(
            Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}

[TestFixture]
public class GetOrdersByProjectQueryTests
{
    [Test]
    public async Task Happy_ShouldMapDtos()
    {
        var orders = Substitute.For<IOrderRepository>();
        var auth = Substitute.For<IAuthorizationService>();

        var project = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(
            DomainTestFactory.NewCustomer(7), project,
            Guid.NewGuid(), 2, "+71234567890", DeliveryMethod.Pickup, null);
        orders.GetByProjectPagedAsync(project.Id, 1, 20).Returns((new List<Order> { order }, 1));
        auth.CanManageProjectAsync(1, project.Id).Returns(true);

        var handler = new GetOrdersByProjectQueryHandler(orders, auth);
        var result = await handler.Handle(
            new GetOrdersByProjectQuery(project.Id, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Items.Should().ContainSingle().Subject;
        dto.UserId.Should().Be(7);
        dto.ProjectId.Should().Be(project.Id);
    }

    [Test]
    public async Task NoRights_ShouldFail()
    {
        var orders = Substitute.For<IOrderRepository>();
        var auth = Substitute.For<IAuthorizationService>();
        auth.CanManageProjectAsync(999, Arg.Any<Guid>()).Returns(false);

        var handler = new GetOrdersByProjectQueryHandler(orders, auth);
        var result = await handler.Handle(
            new GetOrdersByProjectQuery(Guid.NewGuid(), 999), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await orders.DidNotReceive().GetByProjectPagedAsync(Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>());
    }
}
