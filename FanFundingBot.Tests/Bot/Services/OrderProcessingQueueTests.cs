using FanFundingBot.Bot.Services;
using FanFundingBot.Core.Enums;
using Microsoft.Extensions.Logging;
using Moq;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class OrderProcessingQueueTests
{
    [Test]
    public async Task EnqueueDequeue_SameOrder_ReturnsOrder()
    {
        var loggerMock = new Mock<ILogger<OrderProcessingQueue>>();
        var queue = new OrderProcessingQueue(loggerMock.Object);
        var request = new OrderSaveRequest(
            1, 100, 50, false, null,
            Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);

        await queue.EnqueueAsync(request, CancellationToken.None);
        var dequeued = await queue.TryDequeueAsync(CancellationToken.None);

        Assert.That(dequeued, Is.Not.Null);
        Assert.That(dequeued.UserId, Is.EqualTo(1));
    }

    [Test]
    public async Task Enqueue_MultipleOrders_DequeuesInOrder()
    {
        var loggerMock = new Mock<ILogger<OrderProcessingQueue>>();
        var queue = new OrderProcessingQueue(loggerMock.Object);
        var projectId = Guid.NewGuid();
        var req1 = new OrderSaveRequest(
            1, 100, 50, false, null,
            projectId, new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);
        var req2 = new OrderSaveRequest(
            2, 200, 60, false, null,
            projectId, new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+79876543210", DeliveryMethod.Delivery, "Address");

        await queue.EnqueueAsync(req1, CancellationToken.None);
        await queue.EnqueueAsync(req2, CancellationToken.None);

        var first = await queue.TryDequeueAsync(CancellationToken.None);
        var second = await queue.TryDequeueAsync(CancellationToken.None);

        Assert.That(first!.UserId, Is.EqualTo(1));
        Assert.That(second!.UserId, Is.EqualTo(2));
    }
}