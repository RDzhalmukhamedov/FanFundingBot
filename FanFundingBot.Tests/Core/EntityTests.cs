using FanFundingBot.Core.Entities;
using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Constants;

namespace FanFundingBot.Tests.Core;

[TestFixture]
public class EntityTests
{
    [Test]
    public void Project_ShouldHaveDefaultValues()
    {
        var project = new Project();

        Assert.That(project.Name, Is.EqualTo(string.Empty));
        Assert.That(project.Addons, Is.Not.Null);
        Assert.That(project.Orders, Is.Not.Null);
        Assert.That(project.Addons, Is.Empty);
        Assert.That(project.Orders, Is.Empty);
    }

    [Test]
    public void Project_ShouldSetProperties()
    {
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = "Test Project",
            Description = "Test Description",
            StatusId = KnownProjectStatusIds.Translation
        };

        Assert.Multiple(() =>
        {
            Assert.That(project.Name, Is.EqualTo("Test Project"));
            Assert.That(project.Description, Is.EqualTo("Test Description"));
            Assert.That(project.StatusId, Is.EqualTo(KnownProjectStatusIds.Translation));
        });
    }

    [Test]
    public void Addon_ShouldSetProperties()
    {
        var projectId = Guid.NewGuid();
        var addon = new Addon
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = "Test Addon",
            Description = "Addon Description",
            Price = 100.50m
        };

        Assert.Multiple(() =>
        {
            Assert.That(addon.ProjectId, Is.EqualTo(projectId));
            Assert.That(addon.Name, Is.EqualTo("Test Addon"));
            Assert.That(addon.Price, Is.EqualTo(100.50m));
            Assert.That(addon.Description, Is.EqualTo("Addon Description"));
        });
    }

    [Test]
    public void User_ShouldSetProperties()
    {
        var user = new Customer
        {
            Id = 12345,
            FirstName = "John",
            LastName = "Doe",
            Username = "johndoe",
            Phone = "+71234567890",
            Role = UserRole.Regular
        };

        Assert.Multiple(() =>
        {
            Assert.That(user.Id, Is.EqualTo(12345));
            Assert.That(user.FirstName, Is.EqualTo("John"));
            Assert.That(user.LastName, Is.EqualTo("Doe"));
            Assert.That(user.Username, Is.EqualTo("johndoe"));
            Assert.That(user.Phone, Is.EqualTo("+71234567890"));
            Assert.That(user.Role, Is.EqualTo(UserRole.Regular));
        });
    }

    [Test]
    public void Order_ShouldHaveDefaultStatus()
    {
        var order = new Order();
        Assert.That(order.Status, Is.EqualTo(OrderStatus.New));
    }

    [Test]
    public void Order_ShouldSetDeliveryProperties()
    {
        var order = new Order
        {
            Phone = "+71234567890",
            DeliveryMethod = DeliveryMethod.Delivery,
            Address = "Test City, Test St, 123",
            Status = OrderStatus.Confirmed
        };

        Assert.Multiple(() =>
        {
            Assert.That(order.Phone, Is.EqualTo("+71234567890"));
            Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Delivery));
            Assert.That(order.Address, Is.EqualTo("Test City, Test St, 123"));
            Assert.That(order.Status, Is.EqualTo(OrderStatus.Confirmed));
        });
    }

    [Test]
    public void Order_WithPickup_ShouldNotRequireAddress()
    {
        var order = new Order
        {
            DeliveryMethod = DeliveryMethod.Pickup,
            Address = null
        };

        Assert.That(order.Address, Is.Null);
        Assert.That(order.DeliveryMethod, Is.EqualTo(DeliveryMethod.Pickup));
    }

    [Test]
    public void OrderAddon_ShouldSetCompositeKey()
    {
        var orderId = Guid.NewGuid();
        var addonId = Guid.NewGuid();

        var orderAddon = new OrderAddon
        {
            OrderId = orderId,
            AddonId = addonId
        };

        Assert.Multiple(() =>
        {
            Assert.That(orderAddon.OrderId, Is.EqualTo(orderId));
            Assert.That(orderAddon.AddonId, Is.EqualTo(addonId));
        });
    }

    [Test]
    public void OrderAddon_ShouldHaveDefaultQuantity()
    {
        var orderAddon = new OrderAddon();
        Assert.That(orderAddon.Quantity, Is.EqualTo(1));
    }

    [Test]
    public void ProjectStatusEntry_ShouldHaveDefaultCanEdit()
    {
        var entry = new ProjectStatus();
        Assert.That(entry.CanEditOrders, Is.False);
    }

    [Test]
    public void ProjectStatusEntry_ShouldSetCanEdit()
    {
        var entry = new ProjectStatus
        {
            Id = 1,
            DisplayName = "Test",
            Order = 1,
            CanEdit = true
        };

        Assert.That(entry.CanEditOrders, Is.True);
    }

    [Test]
    public void Question_ShouldHaveNoAnswerByDefault()
    {
        var question = new Question();
        Assert.That(question.Answer, Is.Null);
        Assert.That(question.AnsweredAt, Is.Null);
    }

    [Test]
    public void Question_ShouldSetAnswer()
    {
        var answeredAt = DateTime.UtcNow;
        var question = new Question
        {
            Text = "Test question?",
            Answer = "Test answer!",
            AnsweredAt = answeredAt
        };

        Assert.Multiple(() =>
        {
            Assert.That(question.Text, Is.EqualTo("Test question?"));
            Assert.That(question.Answer, Is.EqualTo("Test answer!"));
            Assert.That(question.AnsweredAt, Is.EqualTo(answeredAt));
        });
    }

    [Test]
    public void Project_ShouldManageAddons()
    {
        var project = new Project();
        var addon1 = new Addon { Id = Guid.NewGuid(), Name = "A1" };
        var addon2 = new Addon { Id = Guid.NewGuid(), Name = "A2" };

        project.Addons.Add(addon1);
        project.Addons.Add(addon2);

        Assert.That(project.Addons, Has.Count.EqualTo(2));
        Assert.That(project.Addons, Contains.Item(addon1));
        Assert.That(project.Addons, Contains.Item(addon2));
    }

    [Test]
    public void User_ShouldManageOrders()
    {
        var user = new Customer { Id = 1 };
        var order = new Order { Id = Guid.NewGuid(), UserId = 1 };

        user.Orders.Add(order);

        Assert.That(user.Orders, Has.Count.EqualTo(1));
        Assert.That(user.Orders, Contains.Item(order));
        Assert.That(order.User, Is.Null);
    }
}
