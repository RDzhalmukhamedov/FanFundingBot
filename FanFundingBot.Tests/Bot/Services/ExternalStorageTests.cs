using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Infrastructure.ExternalStorage;
using FanFundingBot.Tests;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class FileOrderStorageTests
{
    [Test]
    public async Task SaveOrderAsync_ShouldCreateFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileOrderStorage(tempDir);

        var order = DomainTestFactory.NewFormed();

        await storage.SaveOrderAsync(order);

        var filePath = Path.Combine(tempDir, "orders.json");
        Assert.That(File.Exists(filePath), Is.True);

        var json = await File.ReadAllTextAsync(filePath);
        Assert.That(json, Does.Contain(order.Id.ToString()));

        // Cleanup
        Directory.Delete(tempDir, true);
    }

    [Test]
    public async Task SaveOrderAsync_MultipleOrders_ShouldAppend()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileOrderStorage(tempDir);

        var order1 = DomainTestFactory.NewFormed(DomainTestFactory.NewCustomer(1));
        var order2 = DomainTestFactory.NewFormed(DomainTestFactory.NewCustomer(2));

        await storage.SaveOrderAsync(order1);
        await storage.SaveOrderAsync(order2);

        var filePath = Path.Combine(tempDir, "orders.json");
        var json = await File.ReadAllTextAsync(filePath);

        Assert.That(json, Does.Contain(order1.Id.ToString()));
        Assert.That(json, Does.Contain(order2.Id.ToString()));

        Directory.Delete(tempDir, true);
    }

    [Test]
    public async Task DeleteOrderAsync_ShouldRemoveOrder()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileOrderStorage(tempDir);

        var order = DomainTestFactory.NewFormed();

        await storage.SaveOrderAsync(order);
        await storage.DeleteOrderAsync(order.Id);

        var filePath = Path.Combine(tempDir, "orders.json");
        var json = await File.ReadAllTextAsync(filePath);

        Assert.That(json, Does.Not.Contain(order.Id.ToString()));

        Directory.Delete(tempDir, true);
    }

    [Test]
    public async Task DeleteOrderAsync_ShouldNotThrow_WhenOrderNotFound()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileOrderStorage(tempDir);

        Assert.DoesNotThrowAsync(async () => await storage.DeleteOrderAsync(Guid.NewGuid()));

        Directory.Delete(tempDir, true);
    }

    [Test]
    public async Task SaveOrderAsync_ShouldStoreQuantityAsNumber()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var storage = new FileOrderStorage(tempDir);

        var order = DomainTestFactory.NewFormed(quantity: 2);

        await storage.SaveOrderAsync(order);

        var filePath = Path.Combine(tempDir, "orders.json");
        var json = await File.ReadAllTextAsync(filePath);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("\"Quantity\": 2"));
            Assert.That(json, Does.Not.Contain("Quantity { }"));
        });

        Directory.Delete(tempDir, true);
    }
}