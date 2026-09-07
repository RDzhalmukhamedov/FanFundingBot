using FanFundingBot.App.Services;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Telegram.Bot;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class BackgroundOrderProcessorTests
{
    [Test]
    public async Task ProcessOrder_CreatesOrderAndNotifiesUser()
    {
        var loggerMock = new Mock<ILogger<OrderProcessingQueue>>();
        var queue = new OrderProcessingQueue(loggerMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var botClientMock = new Mock<ITelegramBotClient>();
        var orderFlowMock = new Mock<IOrderFlowService>();
        var processorLoggerMock = new Mock<ILogger<BackgroundOrderProcessor>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(s => s.GetService(typeof(IOrderFlowService)))
            .Returns(orderFlowMock.Object);
        serviceProviderMock.Setup(s => s.GetService(typeof(ITelegramBotClient)))
            .Returns(botClientMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processor = new BackgroundOrderProcessor(queue, scopeFactoryMock.Object, processorLoggerMock.Object);

        var request = new OrderSaveRequest(
            1, 100, 50, false, null,
            Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);
        await queue.EnqueueAsync(request, CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await processor.StartAsync(cts.Token);
        await Task.Delay(200);

        orderFlowMock.Verify(s => s.CreateOrderAsync(
            It.IsAny<long>(), It.IsAny<Guid>(),
            It.IsAny<HashSet<Guid>>(), It.IsAny<Dictionary<Guid, int>>(),
            It.IsAny<string>(), It.IsAny<DeliveryMethod>(), It.IsAny<string?>()),
            Times.Once);
    }

    [Test]
    public async Task ProcessOrder_UpdateOrder_WhenIsEditing()
    {
        var loggerMock = new Mock<ILogger<OrderProcessingQueue>>();
        var queue = new OrderProcessingQueue(loggerMock.Object);
        var scopeFactoryMock = new Mock<IServiceScopeFactory>();
        var botClientMock = new Mock<ITelegramBotClient>();
        var orderFlowMock = new Mock<IOrderFlowService>();
        var processorLoggerMock = new Mock<ILogger<BackgroundOrderProcessor>>();

        var serviceProviderMock = new Mock<IServiceProvider>();
        serviceProviderMock.Setup(s => s.GetService(typeof(IOrderFlowService)))
            .Returns(orderFlowMock.Object);
        serviceProviderMock.Setup(s => s.GetService(typeof(ITelegramBotClient)))
            .Returns(botClientMock.Object);
        var scopeMock = new Mock<IServiceScope>();
        scopeMock.Setup(s => s.ServiceProvider).Returns(serviceProviderMock.Object);
        scopeFactoryMock.Setup(f => f.CreateScope()).Returns(scopeMock.Object);

        var processor = new BackgroundOrderProcessor(queue, scopeFactoryMock.Object, processorLoggerMock.Object);

        var request = new OrderSaveRequest(
            1, 100, 50, true, Guid.NewGuid(),
            Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);
        await queue.EnqueueAsync(request, CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await processor.StartAsync(cts.Token);
        await Task.Delay(200);

        orderFlowMock.Verify(s => s.UpdateOrderAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(),
            It.IsAny<HashSet<Guid>>(), It.IsAny<Dictionary<Guid, int>>(),
            It.IsAny<string>(), It.IsAny<DeliveryMethod>(), It.IsAny<string?>()),
            Times.Once);
    }
}