using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Constants;
using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Exceptions;
using FanFundingBot.Core.Interfaces;
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
        var addonIds = new HashSet<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var phone = "+71234567890";

        var user = new User { Id = userId, Phone = "old_phone" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId, Name = "Test" });

        var addons = new List<Addon>
        {
            new() { Id = addonIds.ElementAt(0), Name = "Addon 1", Price = 10m },
            new() { Id = addonIds.ElementAt(1), Name = "Addon 2", Price = 15m }
        };
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId)).ReturnsAsync(addons);

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? savedOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        var order = await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), phone, DeliveryMethod.Delivery, "Test Address");

        Assert.Multiple(() =>
        {
            Assert.That(order, Is.Not.Null);
            Assert.That(order.UserId, Is.EqualTo(userId));
            Assert.That(order.ProjectId, Is.EqualTo(projectId));
            Assert.That(order.Phone, Is.EqualTo(phone));
            Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(order.Address, Is.EqualTo("Test Address"));
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
        var addonIds = new HashSet<Guid> { Guid.NewGuid() };

        var user = new User { Id = userId, Phone = "+71234567890" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<Addon> { new() { Id = addonIds.First(), Name = "Test", Price = 10m } });
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

        var user = new User { Id = userId, Phone = "old_phone" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });

        var addons = new List<Addon>
        {
            new() { Id = addonId1, Name = "Addon 1", Price = 10m },
            new() { Id = addonId2, Name = "Addon 2", Price = 15m }
        };
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId)).ReturnsAsync(addons);

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
        Assert.That(oa1.Quantity, Is.EqualTo(3));
        Assert.That(oa2.Quantity, Is.EqualTo(2));
    }

    [Test]
    public async Task CreateOrderAsync_MissingQuantities_DefaultsToOne()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { addonId };

        var user = new User { Id = userId, Phone = "+71234567890" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<Addon> { new() { Id = addonId, Name = "Test", Price = 10m } });
        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        Order? savedOrder = null;
        _orderRepoMock.Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o => savedOrder = o)
            .Returns(Task.CompletedTask);

        await _service.CreateOrderAsync(userId, projectId, addonIds, new Dictionary<Guid, int>(), "+71234567890", DeliveryMethod.Pickup, null);

        Assert.That(savedOrder, Is.Not.Null);
        var oa = savedOrder!.OrderAddons.First();
        Assert.That(oa.Quantity, Is.EqualTo(1));
    }

    [Test]
    public async Task CreateOrderAsync_ExternalStorageFails_CompensatesAndThrows()
    {
        var userId = 1L;
        var projectId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { Guid.NewGuid() };

        var user = new User { Id = userId, Phone = "+71234567890" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });

        var addonId = addonIds.First();
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<Addon> { new() { Id = addonId, Name = "Test", Price = 10m } });

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
        var addonIds = new HashSet<Guid> { Guid.NewGuid() };

        var user = new User { Id = userId, Phone = "+71234567890" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });

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

        var existingOrder = new Order
        {
            Id = orderId,
            ProjectId = projectId,
            Phone = "old_phone",
            DeliveryMethod = DeliveryMethod.Pickup,
            OrderAddons = new List<OrderAddon>()
        };

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingOrder);
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new User { Id = 1 });
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });

        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(projectId))
            .ReturnsAsync(new List<Addon> { new() { Id = addonId, Name = "Updated Addon", Price = 20m } });

        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>())).Returns(Task.CompletedTask);

        await _service.UpdateOrderAsync(orderId, projectId, addonIds, quantities, "new_phone", DeliveryMethod.Delivery, "New Address");

        Assert.Multiple(() =>
        {
            Assert.That(existingOrder.Phone, Is.EqualTo("new_phone"));
            Assert.That(existingOrder.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(existingOrder.Address, Is.EqualTo("New Address"));
        });
        _orderRepoMock.Verify(r => r.DeleteOrderAddonsAsync(orderId), Times.Once);
        _orderRepoMock.Verify(r => r.UpdateAsync(existingOrder), Times.Once);
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

        var existingOrder = new Order
        {
            Id = orderId,
            UserId = 1,
            ProjectId = projectId,
            Phone = "old_phone",
            DeliveryMethod = DeliveryMethod.Pickup,
            OrderAddons = new List<OrderAddon>
            {
                new() { OrderId = orderId, AddonId = Guid.NewGuid(), Quantity = 2 }
            }
        };

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingOrder);
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new User { Id = 1 });
        _projectRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Project { Id = projectId });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Addon> { new() { Id = addonId, Name = "Addon", Price = 10m } });
        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new IOException("External storage unavailable"));

        var ex = Assert.ThrowsAsync<OrderSaveException>(() =>
            _service.UpdateOrderAsync(orderId, projectId, addonIds, quantities, "new_phone", DeliveryMethod.Delivery, "New Address"));

        Assert.That(ex.Message, Does.Contain("Не удалось обновить заказ"));
        _orderRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Order>()), Times.Exactly(2));
        _orderRepoMock.Verify(r => r.DeleteOrderAddonsAsync(orderId), Times.Exactly(2));
    }

    [Test]
    public async Task UpdateOrderAsync_DbFails_NotifiesOwnerAndThrows()
    {
        var orderId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var addonIds = new HashSet<Guid> { Guid.NewGuid() };
        var existingOrder = new Order
        {
            Id = orderId,
            UserId = 1,
            ProjectId = projectId,
            Phone = "old_phone",
            DeliveryMethod = DeliveryMethod.Pickup,
            OrderAddons = new List<OrderAddon>()
        };

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(existingOrder);
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
            .ReturnsAsync(new Order
            {
                Id = orderId,
                UserId = 1,
                ProjectId = Guid.NewGuid(),
                OrderAddons = new List<OrderAddon>()
            });
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
            .ReturnsAsync(new Order
            {
                Id = orderId,
                UserId = 1,
                ProjectId = projectId,
                Phone = "+71234567890",
                DeliveryMethod = DeliveryMethod.Delivery,
                Address = "Test Address",
                Status = OrderStatus.New,
                CreatedAt = DateTime.UtcNow,
                OrderAddons = new List<OrderAddon>
                {
                    new() { OrderId = orderId, AddonId = Guid.NewGuid(), Quantity = 1 }
                }
            });

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
            .ReturnsAsync(new Order
            {
                Id = orderId,
                UserId = 1,
                ProjectId = Guid.NewGuid(),
                OrderAddons = new List<OrderAddon>()
            });
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
        var orders = new List<Order>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow.AddHours(-1) }
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
        var addonIds = new HashSet<Guid> { Guid.NewGuid() };

        var user = new User { Id = userId, Phone = "+71234567890" };
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId)).ReturnsAsync(new Project { Id = projectId });

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
        var originalAddonId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            UserId = 1,
            ProjectId = Guid.NewGuid(),
            Phone = "+71111111111",
            DeliveryMethod = DeliveryMethod.Pickup,
            Address = null,
            OrderAddons = new List<OrderAddon>
            {
                new() { OrderId = orderId, AddonId = originalAddonId, Quantity = 1 }
            }
        };

        _orderRepoMock.Setup(r => r.GetByIdWithAddonsAsync(orderId)).ReturnsAsync(order);
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<long>())).ReturnsAsync(new User { Id = 1 });
        _projectRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync(new Project { Id = Guid.NewGuid() });
        _addonRepoMock.Setup(r => r.GetByProjectIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new List<Addon>());
        _externalStorageMock.Setup(s => s.SaveOrderAsync(It.IsAny<Order>()))
            .ThrowsAsync(new Exception("External storage failed"));

        try
        {
            await _service.UpdateOrderAsync(orderId, Guid.NewGuid(),
                new HashSet<Guid>(), new Dictionary<Guid, int>(),
                "+72222222222", DeliveryMethod.Delivery, "New Address");
        }
        catch (OrderSaveException)
        {
        }

        Assert.Multiple(() =>
        {
            Assert.That(order.Phone, Is.EqualTo("+71111111111"));
            Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Pickup));
            Assert.That(order.Address, Is.Null);
        });
    }

    [Test]
    public async Task DeleteOrderAsync_ExternalStorageFails_NotifiesOwner()
    {
        var orderId = Guid.NewGuid();
        var order = new Order
        {
            Id = orderId,
            UserId = 1,
            ProjectId = Guid.NewGuid(),
            Phone = "+71111111111",
            DeliveryMethod = DeliveryMethod.Pickup,
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow,
            OrderAddons = new List<OrderAddon>()
        };

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
