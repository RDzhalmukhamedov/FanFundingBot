using System.Text.Json;
using FanFundingBot.Application.Abstractions;
using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Interfaces;
using FanFundingBot.Infrastructure.Persistence;
using FanFundingBot.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;

namespace FanFundingBot.App.Services;

public class OutboxProcessor : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(IServiceScopeFactory scopes, ILogger<OutboxProcessor> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox batch processing failed");
            }

            try
            {
                await Task.Delay(PollDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("OutboxProcessor stopped");
    }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var orders = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IOrderExternalStorage>();

        var now = DateTime.UtcNow;

        List<OutboxMessage> messages;
        if (dbContext.Database.IsNpgsql())
        {
            // Атомарный захват пачки между репликами: пропуск заблокированных строк.
            messages = await dbContext.OutboxMessages
                .FromSqlRaw(
                    """
                    SELECT * FROM "OutboxMessages"
                    WHERE "Processed" = false AND ("NextRetryOn" IS NULL OR "NextRetryOn" <= {0})
                    ORDER BY "OccurredOn" LIMIT {1}
                    FOR UPDATE SKIP LOCKED
                    """,
                    now, BatchSize)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // InMemory (тесты) не поддерживает FromSql: та же выборка через LINQ.
            messages = await dbContext.OutboxMessages
                .Where(m => !m.Processed && (m.NextRetryOn == null || m.NextRetryOn <= now))
                .OrderBy(m => m.OccurredOn)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);
        }

        foreach (var message in messages)
        {
            try
            {
                await DispatchAsync(message, orders, storage, cancellationToken);
                message.MarkProcessed();
            }
            catch (Exception ex)
            {
                message.MarkFailed(ex);
                _logger.LogError(ex, "Outbox dispatch failed for {MessageId} ({MessageType})",
                    message.Id, message.Type);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return messages.Count;
    }

    private static async Task DispatchAsync(
        OutboxMessage message,
        IOrderRepository orders,
        IOrderExternalStorage storage,
        CancellationToken cancellationToken)
    {
        var eventType = Type.GetType(message.Type);
        if (eventType is null)
            return;

        if (JsonSerializer.Deserialize(message.Payload, eventType) is not IDomainEvent domainEvent)
            return;

        switch (domainEvent)
        {
            case OrderFormedEvent formed:
                var order = await orders.GetByIdWithAddonsAsync(formed.OrderId);
                if (order is null)
                    return;
                await storage.SaveOrderAsync(order, cancellationToken);
                break;

            case OrderDeletedEvent deleted:
                await storage.DeleteOrderAsync(deleted.OrderId, cancellationToken);
                break;

            default:
                break;
        }
    }
}
