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
}
