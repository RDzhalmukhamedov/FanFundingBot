using FanFundingBot.App.Services;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Entities;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Infrastructure.Persistence;
using FanFundingBot.Infrastructure.Persistence.Outbox;
using FanFundingBot.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FanFundingBot.Tests.Infrastructure;

[TestFixture]
public class DomainEventInterceptorTests
{
    private static AppDbContext CreateContext(DomainEventInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString());
        if (interceptor is not null)
            builder.AddInterceptors(interceptor);
        return new AppDbContext(builder.Options);
    }

    [Test]
    public async Task SavingOrderWithEvents_WritesOutboxAndClears()
    {
        using var context = CreateContext(new DomainEventInterceptor());

        var user = DomainTestFactory.NewCustomer(1);
        var project = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(user, project);
        order.DomainEvents.Should().NotBeEmpty();
        project.ClearDomainEvents();

        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var messages = await context.Set<OutboxMessage>().ToListAsync();
        messages.Should().ContainSingle();
        messages[0].Type.Should().Contain(nameof(OrderFormedEvent));
        messages[0].Processed.Should().BeFalse();
        order.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public async Task SavingWithoutEvents_WritesNothing()
    {
        using var context = CreateContext(new DomainEventInterceptor());

        context.Users.Add(DomainTestFactory.NewCustomer(1));
        await context.SaveChangesAsync();

        var messages = await context.Set<OutboxMessage>().ToListAsync();
        messages.Should().BeEmpty();
    }

    [Test]
    public async Task NoInterceptor_NoOutboxRows()
    {
        using var context = CreateContext();

        var user = DomainTestFactory.NewCustomer(1);
        var project = DomainTestFactory.NewProject();
        context.Orders.Add(DomainTestFactory.NewFormed(user, project));
        await context.SaveChangesAsync();

        var messages = await context.Set<OutboxMessage>().ToListAsync();
        messages.Should().BeEmpty();
    }
}

[TestFixture]
public class OutboxProcessorTests
{
    private ServiceProvider _provider = null!;
    private string _dbName = string.Empty;
    private IOrderExternalStorage _storage = null!;

    [SetUp]
    public void Setup()
    {
        _dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o
            .UseInMemoryDatabase(_dbName)
            .AddInterceptors(new DomainEventInterceptor()));
        services.AddScoped<IOrderRepository, OrderRepository>();
        _storage = Substitute.For<IOrderExternalStorage>();
        services.AddScoped(_ => _storage);

        _provider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown() => _provider?.Dispose();

    private OutboxProcessor CreateProcessor()
        => new(_provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessor>.Instance);

    private AppDbContext CreateContext()
    {
        var scope = _provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        context.Database.EnsureCreated();
        return context;
    }

    [Test]
    public async Task EmptyBatch_ShouldReturnZero()
    {
        using var _ = CreateContext();
        var processor = CreateProcessor();

        var count = await processor.ProcessBatchAsync(CancellationToken.None);

        count.Should().Be(0);
    }

    [Test]
    public async Task OrderFormed_ShouldDispatchAndMarkProcessed()
    {
        using var context = CreateContext();
        var user = DomainTestFactory.NewCustomer(1);
        var project = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(user, project);
        project.ClearDomainEvents();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        var count = await processor.ProcessBatchAsync(CancellationToken.None);

        count.Should().Be(1);
        await _storage.Received(1).SaveOrderAsync(
            Arg.Is<Order>(o => o.Id == order.Id), Arg.Any<CancellationToken>());

        using var verifyContext = CreateContext();
        var messages = await verifyContext.Set<OutboxMessage>().ToListAsync();
        messages.Should().ContainSingle();
        messages[0].Processed.Should().BeTrue();
    }

    [Test]
    public async Task StorageFailure_ShouldRetryAndStayUnprocessed()
    {
        using var context = CreateContext();
        var user = DomainTestFactory.NewCustomer(1);
        var project = DomainTestFactory.NewProject();
        var formed = DomainTestFactory.NewFormed(user, project);
        project.ClearDomainEvents();
        context.Orders.Add(formed);
        await context.SaveChangesAsync();

        _storage.When(s => s.SaveOrderAsync(Arg.Any<Order>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new IOException("sheets down"));

        var processor = CreateProcessor();
        var count = await processor.ProcessBatchAsync(CancellationToken.None);

        count.Should().Be(1);
        using var verifyContext = CreateContext();
        var message = await verifyContext.Set<OutboxMessage>().SingleAsync();
        message.Processed.Should().BeFalse();
        message.RetryCount.Should().Be(1);
    }

    [Test]
    public async Task DeletedOrder_ShouldDeleteFromStorage()
    {
        using var context = CreateContext();
        var goneId = Guid.NewGuid();
        context.Set<OutboxMessage>().Add(OutboxMessage.From(new OrderDeletedEvent(goneId)));
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchAsync(CancellationToken.None);

        await _storage.Received(1).DeleteOrderAsync(goneId, Arg.Any<CancellationToken>());
        using var verifyContext = CreateContext();
        var message = await verifyContext.Set<OutboxMessage>().SingleAsync();
        message.Processed.Should().BeTrue();
    }

    [Test]
    public async Task OrderStatusChanged_ShouldUpdateInStorage()
    {
        using var context = CreateContext();
        var user = DomainTestFactory.NewCustomer(1);
        var project = DomainTestFactory.NewProject();
        var order = DomainTestFactory.NewFormed(user, project);
        project.ClearDomainEvents();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        order.SetStatus(Domain.Enums.OrderStatus.Confirmed);
        context.Orders.Update(order);
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        var count = await processor.ProcessBatchAsync(CancellationToken.None);

        count.Should().Be(2);
        await _storage.Received(2).SaveOrderAsync(
            Arg.Is<Order>(o => o.Id == order.Id), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task OrderStatusChanged_MissingOrder_ShouldSkip()
    {
        using var context = CreateContext();
        var goneId = Guid.NewGuid();
        context.Set<OutboxMessage>().Add(OutboxMessage.From(
            new OrderStatusChangedEvent(goneId, Domain.Enums.OrderStatus.New, Domain.Enums.OrderStatus.Confirmed)));
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchAsync(CancellationToken.None);

        await _storage.DidNotReceive().SaveOrderAsync(
            Arg.Any<Order>(), Arg.Any<CancellationToken>());
        using var verifyContext = CreateContext();
        var message = await verifyContext.Set<OutboxMessage>().SingleAsync();
        message.Processed.Should().BeTrue();
    }

    [Test]
    public async Task UnknownType_ShouldMarkProcessedWithoutStorageCall()
    {
        using var context = CreateContext();
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Type))!
            .SetValue(message, "Bogus.Type, Bogus");
        context.Set<OutboxMessage>().Add(message);
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchAsync(CancellationToken.None);

        await _storage.DidNotReceive().SaveOrderAsync(
            Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteOrderAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        using var verifyContext = CreateContext();
        (await verifyContext.Set<OutboxMessage>().SingleAsync()).Processed.Should().BeTrue();
    }

    [Test]
    public async Task UndecodablePayload_ShouldMarkProcessedWithoutStorageCall()
    {
        using var context = CreateContext();
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Payload))!
            .SetValue(message, "not-json");
        context.Set<OutboxMessage>().Add(message);
        await context.SaveChangesAsync();

        var processor = CreateProcessor();
        await processor.ProcessBatchAsync(CancellationToken.None);

        await _storage.DidNotReceive().SaveOrderAsync(
            Arg.Any<Order>(), Arg.Any<CancellationToken>());
        await _storage.DidNotReceive().DeleteOrderAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        using var verifyContext = CreateContext();
        (await verifyContext.Set<OutboxMessage>().SingleAsync()).Processed.Should().BeTrue();
    }
}

[TestFixture]
public class OutboxMessageTests
{
    [Test]
    public void From_ShouldBeUnprocessed()
    {
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));

        message.Processed.Should().BeFalse();
        message.RetryCount.Should().Be(0);
        message.Type.Should().Contain(nameof(OrderDeletedEvent));
    }

    [Test]
    public void MarkProcessed_ShouldSetFlags()
    {
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));

        message.MarkProcessed();

        message.Processed.Should().BeTrue();
        message.ProcessedOn.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Test]
    public void MarkFailed_ShouldBackoffExponentiallyCappedAt10Min()
    {
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));
        var expectedDelays = new[] { 5, 10, 20, 40, 80, 160, 320, 600, 600 };

        for (var i = 0; i < expectedDelays.Length; i++)
        {
            var before = DateTime.UtcNow;
            message.MarkFailed(new IOException("boom"));

            message.Processed.Should().BeFalse();
            message.RetryCount.Should().Be(i + 1);
            message.LastError.Should().Be("boom");
            message.NextRetryOn.Should().BeOnOrAfter(before.AddSeconds(expectedDelays[i] - 1));
            message.NextRetryOn.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(expectedDelays[i] + 5));
        }
    }

    [Test]
    public void MarkFailed_WithoutException_ShouldKeepLastError()
    {
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));
        message.MarkFailed(new IOException("first"));
        message.MarkFailed();

        message.RetryCount.Should().Be(2);
        message.LastError.Should().Be("first");
        message.NextRetryOn.Should().NotBeNull();
    }

    [Test]
    public void MarkFailed_ShouldTruncateLastErrorTo2000()
    {
        var message = OutboxMessage.From(new OrderDeletedEvent(Guid.NewGuid()));

        message.MarkFailed(new IOException(new string('x', 2500)));

        message.LastError.Should().HaveLength(2000);
    }
}
