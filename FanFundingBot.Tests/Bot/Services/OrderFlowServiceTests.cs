using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Domain.ValueObjects;
using Moq;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class OrderFlowServiceTests
{
    private Mock<IOrderRepository> _orderRepoMock;
    private Mock<IProjectRepository> _projectRepoMock;
    private Mock<IAddonRepository> _addonRepoMock;
    private Mock<IUserRepository> _userRepoMock;
    private Mock<IOrderExternalStorage> _externalStorageMock;
    private OwnerNotificationDelegate _notifyOwner;
    private OrderFlowService _service;

    private static User NewUser(long userId = 1, string phone = "+71234567890")
    {
        var user = DomainTestFactory.NewCustomer(userId);
        user.SetPhone(phone);
        return user;
    }

    private static Project NewProject(Guid? projectId = null)
    {
        return DomainTestFactory.NewProjectWithId(projectId ?? Guid.NewGuid());
    }

    private static Addon NewAddon(Guid id, string name = "Addon", decimal price = 10m)
        => DomainTestFactory.NewAddon(id, name, price);

    private static FormedOrder NewStoredOrder(
        Guid orderId, long userId, Guid projectId, Guid addonId, int quantity = 1, string phone = "+71234567890")
    {
        var customer = NewUser(userId, phone);
        var project = NewProject(projectId);
        var draft = new DraftOrder(customer, project);
        draft.AddAddon(NewAddon(addonId), quantity);
        draft.SetPhoneNumber(phone);
        draft.SetDeliveryMethod(DeliveryMethod.Pickup);
        return draft.Form();
    }

    [SetUp]
    public void Setup()
    {
        _orderRepoMock = new Mock<IOrderRepository>();
        _projectRepoMock = new Mock<IProjectRepository>();
        _addonRepoMock = new Mock<IAddonRepository>();
        _userRepoMock = new Mock<IUserRepository>();
        _externalStorageMock = new Mock<IOrderExternalStorage>();
        _notifyOwner = (_) => Task.CompletedTask;

        _service = new OrderFlowService(
            _orderRepoMock.Object,
            _projectRepoMock.Object,
            _addonRepoMock.Object,
            _userRepoMock.Object,
            _externalStorageMock.Object,
            _notifyOwner);

        _orderRepoMock
            .Setup(r => r.GetByProjectAndUserAsync(It.IsAny<Guid>(), It.IsAny<long>()))
            .ReturnsAsync(new List<Order>());
    }

    [Test]
    public void ValidatePhone_ValidPhone_ReturnsNull()
    {
        var result = _service.ValidatePhone("+71234567890");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidatePhone_InvalidPhone_ReturnsError()
    {
        var result = _service.ValidatePhone("123");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("корректный"));
    }

    [Test]
    public void ValidatePhone_EmptyPhone_ReturnsError()
    {
        var result = _service.ValidatePhone("");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void ValidateAddress_ValidAddress_ReturnsNull()
    {
        var result = _service.ValidateAddress("г Москва, ул Ленина, д 1");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void ValidateAddress_EmptyAddress_ReturnsError()
    {
        var result = _service.ValidateAddress("");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void ValidateAddress_TooShort_ReturnsError()
    {
        var result = _service.ValidateAddress("ab");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task CreateOrderAsync_ShouldCreateOrderWithAddons()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId1 = Guid.NewGuid();
        var addonId2 = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId1, addonId2 };
        var phone = "+71234567890";

        var user = NewUser(userId, "old_phone");
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId1, "Addon 1", 10m));
        project.AddAddon(NewAddon(addonId2, "Addon 2", 15m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? savedOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        var order = await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), phone, DeliveryMethod.Delivery, "Test Address");

        Assert.Multiple(() =>
        {
            Assert.That(order, Is.Not.Null);
            Assert.That(order.User.Id, Is.EqualTo(userId));
            Assert.That(order.Project.Id, Is.EqualTo(projectId));
            Assert.That(order.Phone.ToString(), Is.EqualTo(phone));
            Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(order.Address.ToString(), Is.EqualTo("Test Address"));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.New));
            Assert.That(order.OrderAddons, Has.Count.EqualTo(2));
            Assert.That(order.OrderAddons.All(oa => oa.Addon != null), Is.True);
        });

        _userRepoMock.Verify(r => r.UpdateAsync(It.Is<User>(u => u.Phone == phone)), Times.Once);
        _externalStorageMock.Verify(s => s.SaveOrderAsync(It.IsAny<Order>()), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_Pickup_ShouldNotSetAddress()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = NewUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId, "Test", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);
        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        var order = await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null);

        Assert.Multiple(() =>
        {
            Assert.That(order.Address, Is.Null);
            Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Pickup));
        });
    }

    [Test]
    public async Task CreateOrderAsync_WithQuantities_SetsOrderAddonQuantities()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId1 = Guid.NewGuid();
        var addonId2 = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId1, addonId2 };
        var quantities = new Dictionary<Guid, int> { { addonId1, 3 }, { addonId2, 2 } };
        var phone = "+71234567890";

        var user = NewUser(userId, "old_phone");
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId1, "Addon 1", 10m));
        project.AddAddon(NewAddon(addonId2, "Addon 2", 15m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? savedOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        await _service.CreateOrderAsync(userId, projectId, addonIds, quantities, phone, DeliveryMethod.Pickup, null);

        Assert.That(savedOrder, Is.Not.Null);
        Assert.That(savedOrder!.OrderAddons, Has.Count.EqualTo(2));
        var oa1 = savedOrder.OrderAddons.First(oa => oa.AddonId == addonId1);
        var oa2 = savedOrder.OrderAddons.First(oa => oa.AddonId == addonId2);
        Assert.That(oa1.Quantity, Is.EqualTo(Quantity.Create(3)));
        Assert.That(oa2.Quantity, Is.EqualTo(Quantity.Create(2)));
    }

    [Test]
    public async Task CreateOrderAsync_MissingQuantities_DefaultsToOne()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = NewUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId, "Test", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);
        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? savedOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null);

        Assert.That(savedOrder, Is.Not.Null);
        var oa = savedOrder!.OrderAddons.First();
        Assert.That(oa.Quantity, Is.EqualTo(Quantity.Create(1)));
    }

    [Test]
    public async Task CreateOrderAsync_ExternalStorageFails_CompensatesAndThrows()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = NewUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId, "Test", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);

        var orderId = Guid.Empty;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => orderId = o.Id)
            .Returns(Task.CompletedTask);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new IOException("External storage unavailable"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null));

        Assert.That(ex.Message, Does.Contain("Не удалось сохранить заказ"));
        _orderRepoMock.Verify(r => r.DeleteAsync(orderId), Times.Once);
    }

    [Test]
    public async Task CreateOrderAsync_DbFails_NotifiesOwnerAndThrows()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = NewUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId, "Test", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);

        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null));

        Assert.That(ex.Message, Does.Contain("Не удалось сохранить заказ"));
        _orderRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _externalStorageMock.Verify(s => s.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task UpdateOrderAsync_UpdatesExistingOrder()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };
        var quantities = new Dictionary<Guid, int> { { addonId, 5 } };

        var customer = NewUser(1, "+71111111111");
        var project = NewProject(projectId);
        var existingDraft = new DraftOrder(customer, project);
        existingDraft.SetPhoneNumber("+71111111111");
        existingDraft.SetDeliveryMethod(DeliveryMethod.Pickup);

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingDraft);
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync(project);

        var newAddon = NewAddon(addonId, "Updated Addon", 20m);
        project.AddAddon(newAddon);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? updatedOrder = null;
        _orderRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Order>()))
            .Callback<Order>(o => updatedOrder = o)
            .Returns(Task.CompletedTask);

        await _service.UpdateOrderAsync(orderId, projectId, addonIds, quantities, "+72222222222", DeliveryMethod.Delivery, "New Address");

        Assert.Multiple(() =>
        {
            Assert.That(updatedOrder, Is.Not.Null);
            Assert.That(updatedOrder!.Phone.ToString(), Is.EqualTo("+72222222222"));
            Assert.That(updatedOrder.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(updatedOrder.Address.ToString(), Is.EqualTo("New Address"));
            Assert.That(updatedOrder.OrderAddons, Has.Count.EqualTo(1));
            Assert.That((int)updatedOrder.OrderAddons[0].Quantity, Is.EqualTo(5));
        });
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Once);
        _externalStorageMock.Verify(s => s.SaveOrderAsync(It.IsAny<Order>()), Times.Once);
    }

    [Test]
    public void UpdateOrderAsync_NonExistent_Throws()
    {
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync((Order?)null);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await _service.UpdateOrderAsync(Guid.NewGuid(), Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(), "phone", DeliveryMethod.Pickup, null));
    }

    [Test]
    public async Task UpdateOrderAsync_ExternalStorageFails_CompensatesAndThrows()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };
        var quantities = new Dictionary<Guid, int> { { addonId, 5 } };

        var customer = NewUser(1, "+71111111111");
        var project = NewProject(projectId);
        var oldAddonId = Guid.NewGuid();
        var existingDraft = new DraftOrder(customer, project);
        existingDraft.AddAddon(NewAddon(oldAddonId, "Old", 5m), 2);
        existingDraft.SetPhoneNumber("+71111111111");
        existingDraft.SetDeliveryMethod(DeliveryMethod.Pickup);

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingDraft);

        var currentProject = NewProject(projectId);
        currentProject.AddAddon(NewAddon(oldAddonId, "Old", 5m));
        currentProject.AddAddon(NewAddon(addonId, "Addon", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync(currentProject);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new IOException("External storage unavailable"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.UpdateOrderAsync(orderId, projectId, addonIds, quantities, "+72222222222", DeliveryMethod.Delivery, "New Address"));

        Assert.That(ex.Message, Does.Contain("Не удалось обновить заказ"));
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Exactly(2));
    }

    [Test]
    public async Task UpdateOrderAsync_DbFails_NotifiesOwnerAndThrows()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var customer = NewUser(1, "+71111111111");
        var project = NewProject(projectId);
        var existingDraft = new DraftOrder(customer, project);
        existingDraft.SetPhoneNumber("+71111111111");
        existingDraft.SetDeliveryMethod(DeliveryMethod.Pickup);

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingDraft);

        var currentProject = NewProject(projectId);
        currentProject.AddAddon(NewAddon(addonId, "Addon", 10m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync(currentProject);

        _orderRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("DB update failed"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.UpdateOrderAsync(orderId, projectId, addonIds, new Dictionary<Guid, int>(), "phone", DeliveryMethod.Pickup, null));

        Assert.That(ex.Message, Does.Contain("Не удалось обновить заказ"));
        _externalStorageMock.Verify(s => s.SaveOrderAsync(It.IsAny<Order>()), Times.Never);
    }

    [Test]
    public async Task DeleteOrderAsync_CallsRepositoryAndExternalStorage()
    {
        var orderId = Guid.NewGuid();

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId))
            .ReturnsAsync(NewStoredOrder(orderId, 1, Guid.NewGuid(), Guid.NewGuid()));
        _externalStorageMock.Setup(s => s.DeleteOrderAsync(orderId)).Returns(Task.CompletedTask);

        await _service.DeleteOrderAsync(orderId);

        _orderRepoMock.Verify(r => r.DeleteAsync(orderId), Times.Once);
        _externalStorageMock.Verify(s => s.DeleteOrderAsync(orderId), Times.Once);
    }

    [Test]
    public async Task DeleteOrderAsync_NonExistent_DoesNothing()
    {
        var orderId = Guid.NewGuid();
        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync((Order?)null);

        await _service.DeleteOrderAsync(orderId);

        _orderRepoMock.Verify(r => r.DeleteAsync(It.IsAny<Guid>()), Times.Never);
        _externalStorageMock.Verify(s => s.DeleteOrderAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task DeleteOrderAsync_ExternalStorageFails_CompensatesAndThrows()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId))
            .ReturnsAsync(NewStoredOrder(orderId, 1, projectId, Guid.NewGuid(), 1, "+71234567890"));

        _externalStorageMock.Setup(s => s.DeleteOrderAsync(orderId))
            .ThrowsAsync(new IOException("External storage unavailable"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.DeleteOrderAsync(orderId));

        Assert.That(ex.Message, Does.Contain("Не удалось удалить заказ"));
        _orderRepoMock.Verify(r => r.DeleteAsync(orderId), Times.Once);
        _orderRepoMock.Verify(r => r.AddAsync(It.IsAny<Order>()), Times.Once);
    }

    [Test]
    public async Task DeleteOrderAsync_DbFails_NotifiesOwnerAndThrows()
    {
        var orderId = Guid.NewGuid();

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId))
            .ReturnsAsync(NewStoredOrder(orderId, 1, Guid.NewGuid(), Guid.NewGuid()));
        _orderRepoMock.Setup(r => r.DeleteAsync(orderId))
            .ThrowsAsync(new InvalidOperationException("DB delete failed"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.DeleteOrderAsync(orderId));

        Assert.That(ex.Message, Does.Contain("Не удалось удалить заказ"));
        _externalStorageMock.Verify(s => s.DeleteOrderAsync(It.IsAny<Guid>()), Times.Never);
    }

    [Test]
    public async Task GetLatestOrderAsync_ShouldReturnMostRecent()
    {
        var userId = 1L;
        var customer = NewUser(userId);
        var project = NewProject();
        var orders = new List<Order>
        {
            new DraftOrder(customer, project),
            new DraftOrder(customer, project)
        };

        _orderRepoMock.Setup(r => r.GetByUserAsync(userId)).ReturnsAsync(orders);

        var result = await _service.GetLatestOrderAsync(userId);
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo(orders[0].Id));
    }

    [Test]
    public async Task CreateOrderAsync_AnyError_NotifiesOwner()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = NewUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var project = NewProject(projectId);
        project.AddAddon(NewAddon(addonId));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(projectId)).ReturnsAsync(project);

        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var notifiedMessages = new List<string>();
        _notifyOwner = (msg) => { notifiedMessages.Add(msg); return Task.CompletedTask; };
        _service = new OrderFlowService(
            _orderRepoMock.Object, _projectRepoMock.Object, _addonRepoMock.Object,
            _userRepoMock.Object, _externalStorageMock.Object, _notifyOwner);

        try { await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null); }
        catch { }

        Assert.That(notifiedMessages, Has.Count.EqualTo(1));
        Assert.That(notifiedMessages[0], Does.Contain("DB connection failed"));
    }

    [Test]
    public async Task UpdateOrderAsync_ExternalStorageFails_RestoresOriginalOrder()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var originalAddonId = Guid.NewGuid();
        var newAddonId = Guid.NewGuid();

        var customer = NewUser(1, "+71111111111");
        var project = NewProject(projectId);
        var order = new DraftOrder(customer, project);
        order.AddAddon(NewAddon(originalAddonId, "Original", 5m), 1);
        order.SetPhoneNumber("+71111111111");
        order.SetDeliveryMethod(DeliveryMethod.Pickup);

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(order);

        var currentProject = NewProject(projectId);
        currentProject.AddAddon(NewAddon(originalAddonId, "Original", 5m));
        currentProject.AddAddon(NewAddon(newAddonId, "New", 5m));
        _projectRepoMock.Setup(r => r.GetByIdWithAddonsAsync(It.IsAny<Guid>())).ReturnsAsync(currentProject);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new Exception("External storage failed"));

        var updatedOrders = new List<Order>();
        _orderRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Order>()))
            .Callback<Order>(o => updatedOrders.Add(o))
            .Returns(Task.CompletedTask);

        try
        {
            await _service.UpdateOrderAsync(orderId, projectId,
                new HashSet<Guid> { newAddonId }, new Dictionary<Guid, int>(),
                "+72222222222", DeliveryMethod.Delivery, "New Address");
        }
        catch (OrderSaveException)
        {
        }

        Assert.That(updatedOrders, Has.Count.EqualTo(2));
        var restored = updatedOrders[1];
        Assert.Multiple(() =>
        {
            Assert.That(restored.Phone.ToString(), Is.EqualTo("+71111111111"));
            Assert.That(restored.DeliveryMethod, Is.EqualTo(DeliveryMethod.Pickup));
            Assert.That(restored.Address, Is.Null);
            Assert.That(restored.OrderAddons.Select(p => p.Addon.Id), Does.Contain(originalAddonId));
        });
    }

    [Test]
    public async Task DeleteOrderAsync_ExternalStorageFails_NotifiesOwner()
    {
        var orderId = Guid.NewGuid();
        var order = NewStoredOrder(orderId, 1, Guid.NewGuid(), Guid.NewGuid(), 1, "+71111111111");

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(order);
        _externalStorageMock.Setup(s => s.DeleteOrderAsync(orderId))
            .ThrowsAsync(new Exception("External storage failed"));

        var notifiedMessages = new List<string>();
        _notifyOwner = (msg) => { notifiedMessages.Add(msg); return Task.CompletedTask; };
        _service = new OrderFlowService(
            _orderRepoMock.Object, _projectRepoMock.Object, _addonRepoMock.Object,
            _userRepoMock.Object, _externalStorageMock.Object, _notifyOwner);

        try { await _service.DeleteOrderAsync(orderId); }
        catch { }

        Assert.That(notifiedMessages, Has.Count.EqualTo(1));
        Assert.That(notifiedMessages[0], Does.Contain("External storage failed"));
    }
}
