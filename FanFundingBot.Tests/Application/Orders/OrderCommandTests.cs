using FanFundingBot.Application.Orders.Commands;
using FanFundingBot.Application.Orders.Queries;
using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace FanFundingBot.Tests.Application.Orders;

[TestFixture]
public class CreateOrderCommandTests
{
    private IUserRepository _users;
    private IProjectRepository _projects;
    private IOrderRepository _orders;
    private CreateOrderCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _users = Substitute.For<IUserRepository>();
        _projects = Substitute.For<IProjectRepository>();
        _orders = Substitute.For<IOrderRepository>();
        _handler = new CreateOrderCommandHandler(_users, _projects, _orders);

        _orders.GetByProjectAndUserAsync(Arg.Any<Guid>(), Arg.Any<long>())
            .Returns(new List<Order>());
    }

    private static User NewUser(long id = 1, string phone = "+71234567890")
    {
        var user = DomainTestFactory.NewCustomer(id);
        user.SetPhone(phone);
        return user;
    }

    private static Project NewProjectWith(params Guid[] addonIds)
    {
        var project = DomainTestFactory.NewProject();
        foreach (var id in addonIds)
            project.AddAddon(DomainTestFactory.NewAddon(id));
        return project;
    }

    [Test]
    public async Task Happy_ShouldCreateFormedOrder()
    {
        var addonId = Guid.NewGuid();
        var user = NewUser();
        var project = NewProjectWith(addonId);
        _users.GetByIdAsync(1).Returns(user);
        _projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        Order? saved = null;
        await _orders.AddAsync(Arg.Do<Order>(o => saved = o));

        var result = await _handler.Handle(
            new CreateOrderCommand(1, project.Id, new Dictionary<Guid, int> { [addonId] = 2 },
                "+79998887766", DeliveryMethod.Delivery, "City 1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().NotBeNull();
        saved!.Phone.ToString().Should().Be("+79998887766");
        saved.OrderAddons.Should().HaveCount(1);
        ((int)saved.OrderAddons[0].Quantity).Should().Be(2);
        await _users.Received(1).UpdateAsync(Arg.Any<User>());
    }

    [Test]
    public async Task UserNotFound_ShouldFail()
    {
        _users.GetByIdAsync(Arg.Any<long>()).Returns((User?)null);

        var result = await _handler.Handle(
            new CreateOrderCommand(1, Guid.NewGuid(), new Dictionary<Guid, int>(),
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        await _orders.DidNotReceive().AddAsync(Arg.Any<Order>());
    }

    [Test]
    public async Task DuplicateOrder_ShouldFail()
    {
        _users.GetByIdAsync(1).Returns(NewUser());
        _orders.GetByProjectAndUserAsync(Arg.Any<Guid>(), 1L)
            .Returns(new List<Order> { DomainTestFactory.NewFormed() });

        var result = await _handler.Handle(
            new CreateOrderCommand(1, Guid.NewGuid(), new Dictionary<Guid, int>(),
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("уже есть заказ"));
    }

    [Test]
    public async Task ProjectNotFound_ShouldFail()
    {
        _users.GetByIdAsync(1).Returns(NewUser());
        _projects.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Project?)null);

        var result = await _handler.Handle(
            new CreateOrderCommand(1, Guid.NewGuid(), new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 },
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task ProjectNotEditable_ShouldFail()
    {
        var user = NewUser();
        _users.GetByIdAsync(1).Returns(user);
        var project = new Project();
        project.SetName("P");
        project.SetDescription("D");
        project.SetStatus(new ProjectStatus("Closed", 8, false));
        _projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        var result = await _handler.Handle(
            new CreateOrderCommand(1, project.Id, new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 },
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task AddonMissing_ShouldFail()
    {
        var user = NewUser();
        _users.GetByIdAsync(1).Returns(user);
        var project = NewProjectWith(Guid.NewGuid());
        _projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        var result = await _handler.Handle(
            new CreateOrderCommand(1, project.Id, new Dictionary<Guid, int> { [Guid.NewGuid()] = 1 },
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public void InvalidQuantity_ShouldThrowDomainException()
    {
        var user = NewUser();
        _users.GetByIdAsync(1).Returns(user);
        var addonId = Guid.NewGuid();
        var project = NewProjectWith(addonId);
        _projects.GetByIdWithAddonsAsync(project.Id).Returns(project);

        Assert.ThrowsAsync<DomainException>(async () => await _handler.Handle(
            new CreateOrderCommand(1, project.Id, new Dictionary<Guid, int> { [addonId] = 0 },
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None));
    }
}

[TestFixture]
public class UpdateOrderCommandTests
{
    private IProjectRepository _projects;
    private IOrderRepository _orders;
    private UpdateOrderCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _projects = Substitute.For<IProjectRepository>();
        _orders = Substitute.For<IOrderRepository>();
        _handler = new UpdateOrderCommandHandler(_projects, _orders);
    }

    private static (User user, Project project, FormedOrder order, Guid addonId) ArrangeStored()
    {
        var user = DomainTestFactory.NewCustomer(1);
        user.SetPhone("+71111111111");
        var project = DomainTestFactory.NewProject();
        var addonId = Guid.NewGuid();
        project.AddAddon(DomainTestFactory.NewAddon(addonId, "Old", 5m));
        var order = DomainTestFactory.NewFormed(user, project, addonId, 1, "+71111111111");
        return (user, project, order, addonId);
    }

    [Test]
    public async Task Happy_ShouldReplacePositions()
    {
        var (user, _, order, _) = ArrangeStored();
        var newAddonId = Guid.NewGuid();
        var project = DomainTestFactory.NewProject();
        project.AddAddon(DomainTestFactory.NewAddon(newAddonId, "New", 5m));

        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);
        _projects.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns(project);

        Order? updated = null;
        await _orders.UpdateAsync(Arg.Do<Order>(o => updated = o));

        var result = await _handler.Handle(
            new UpdateOrderCommand(order.Id, user.Id,
                new Dictionary<Guid, int> { [newAddonId] = 3 },
                "+72222222222", DeliveryMethod.Delivery, "City 1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        updated.Should().NotBeNull();
        updated!.Phone.ToString().Should().Be("+72222222222");
        updated.OrderAddons.Should().HaveCount(1);
        updated.OrderAddons[0].Addon.Id.Should().Be(newAddonId);
    }

    [Test]
    public async Task NotFound_ShouldFail()
    {
        _orders.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        var result = await _handler.Handle(
            new UpdateOrderCommand(Guid.NewGuid(), 1, new Dictionary<Guid, int>(),
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task NotOwner_ShouldFail()
    {
        var (_, _, order, _) = ArrangeStored();
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);

        var result = await _handler.Handle(
            new UpdateOrderCommand(order.Id, 999, new Dictionary<Guid, int>(),
                "+71234567890", DeliveryMethod.Pickup, null), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message.Contains("Нет прав"));
        await _orders.DidNotReceive().UpdateAsync(Arg.Any<Order>());
    }
}

[TestFixture]
public class DeleteOrderCommandTests
{
    private IOrderRepository _orders;
    private DeleteOrderCommandHandler _handler;

    [SetUp]
    public void Setup()
    {
        _orders = Substitute.For<IOrderRepository>();
        _handler = new DeleteOrderCommandHandler(_orders);
    }

    [Test]
    public async Task Happy_ShouldDelete()
    {
        var order = DomainTestFactory.NewFormed(DomainTestFactory.NewCustomer(1));
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id, 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        order.DomainEvents.OfType<OrderDeletedEvent>().Should().ContainSingle();
        order.DomainEvents.OfType<OrderDeletedEvent>().Single().OrderId.Should().Be(order.Id);
        await _orders.Received(1).DeleteAsync(order.Id);
    }

    [Test]
    public async Task NotFound_ShouldSucceed()
    {
        _orders.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        var result = await _handler.Handle(new DeleteOrderCommand(Guid.NewGuid(), 1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _orders.DidNotReceive().DeleteAsync(Arg.Any<Guid>());
    }

    [Test]
    public async Task NotOwner_ShouldFail()
    {
        var order = DomainTestFactory.NewFormed(DomainTestFactory.NewCustomer(1));
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);

        var result = await _handler.Handle(new DeleteOrderCommand(order.Id, 999), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }
}

[TestFixture]
public class OrderQueryTests
{
    private IOrderRepository _orders;

    [SetUp]
    public void Setup()
    {
        _orders = Substitute.For<IOrderRepository>();
    }

    [Test]
    public async Task GetMyOrders_ShouldMapDtos()
    {
        var addonId = Guid.NewGuid();
        var order = DomainTestFactory.NewFormed(
            DomainTestFactory.NewCustomer(1), DomainTestFactory.NewProject(),
            addonId, 2, "+71234567890", DeliveryMethod.Delivery, "City 1");
        _orders.GetByUserWithAddonsAsync(1).Returns(new List<Order> { order });

        var handler = new GetMyOrdersQueryHandler(_orders);
        var result = await handler.Handle(new GetMyOrdersQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value.Should().ContainSingle().Subject;
        dto.UserId.Should().Be(1);
        dto.Phone.Should().Be("+71234567890");
        dto.Address.Should().Be("City 1");
        dto.Positions.Should().HaveCount(1);
        dto.Positions[0].AddonId.Should().Be(addonId);
        dto.Positions[0].Quantity.Should().Be(2);
        dto.Total.Should().Be(200m);
    }

    [Test]
    public async Task GetOrderById_NotOwner_ShouldFail()
    {
        var order = DomainTestFactory.NewFormed(DomainTestFactory.NewCustomer(1));
        _orders.GetByIdWithAddonsAsync(order.Id).Returns(order);

        var handler = new GetOrderByIdQueryHandler(_orders);
        var result = await handler.Handle(new GetOrderByIdQuery(order.Id, 999), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }

    [Test]
    public async Task GetOrderById_NotFound_ShouldFail()
    {
        _orders.GetByIdWithAddonsAsync(Arg.Any<Guid>()).Returns((Order?)null);

        var handler = new GetOrderByIdQueryHandler(_orders);
        var result = await handler.Handle(new GetOrderByIdQuery(Guid.NewGuid(), 1), CancellationToken.None);

        result.IsFailed.Should().BeTrue();
    }
}
