using FanFundingBot.App.Services;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Services;
using FanFundingBot.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Telegram.Bot;

namespace FanFundingBot.Tests.Bot.Services;

[TestFixture]
public class BackgroundOrderProcessorTests
{
    private static (BackgroundOrderProcessor processor, OrderProcessingQueue queue, IOrderFlowService orderFlow)
        CreateProcessor()
    {
        var queue = new OrderProcessingQueue(NullLogger<OrderProcessingQueue>.Instance);
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var botClient = Substitute.For<ITelegramBotClient>();
        var orderFlow = Substitute.For<IOrderFlowService>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IOrderFlowService)).Returns(orderFlow);
        serviceProvider.GetService(typeof(ITelegramBotClient)).Returns(botClient);
        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        var processor = new BackgroundOrderProcessor(
            queue, scopeFactory, NullLogger<BackgroundOrderProcessor>.Instance);

        return (processor, queue, orderFlow);
    }

    [Test]
    public async Task ProcessOrder_CreatesOrderAndNotifiesUser()
    {
        var (processor, queue, orderFlow) = CreateProcessor();

        var request = new OrderSaveRequest(
            1, 100, 50, false, null,
            Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);
        await queue.EnqueueAsync(request, CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await processor.StartAsync(cts.Token);
        await Task.Delay(200);

        await orderFlow.Received(1).CreateOrderAsync(
            Arg.Any<long>(), Arg.Any<Guid>(),
            Arg.Any<HashSet<Guid>>(), Arg.Any<Dictionary<Guid, int>>(),
            Arg.Any<string>(), Arg.Any<DeliveryMethod>(), Arg.Any<string?>());
    }

    [Test]
    public async Task ProcessOrder_UpdateOrder_WhenIsEditing()
    {
        var (processor, queue, orderFlow) = CreateProcessor();

        var request = new OrderSaveRequest(
            1, 100, 50, true, Guid.NewGuid(),
            Guid.NewGuid(), new HashSet<Guid>(), new Dictionary<Guid, int>(),
            "+71234567890", DeliveryMethod.Pickup, null);
        await queue.EnqueueAsync(request, CancellationToken.None);

        using var cts = new CancellationTokenSource(100);
        await processor.StartAsync(cts.Token);
        await Task.Delay(200);

        await orderFlow.Received(1).UpdateOrderAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            Arg.Any<HashSet<Guid>>(), Arg.Any<Dictionary<Guid, int>>(),
            Arg.Any<string>(), Arg.Any<DeliveryMethod>(), Arg.Any<string?>());
    }
}
