using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FanFundingBot.Domain.DomainEvents;

namespace FanFundingBot.Infrastructure.Persistence.Outbox;

[ExcludeFromCodeCoverage]
public class OutboxMessage
{
    public Guid Id { get; private set; }
    public DateTime OccurredOn { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public bool Processed { get; private set; }
    public DateTime? ProcessedOn { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? NextRetryOn { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage()
    {
    }

    public static OutboxMessage From(IDomainEvent domainEvent)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredOn = domainEvent.OccurredOn,
            Type = domainEvent.GetType().AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType())
        };
    }

    public void MarkProcessed()
    {
        Processed = true;
        ProcessedOn = DateTime.UtcNow;
    }

    public void MarkFailed()
    {
        MarkFailed(null);
    }

    public void MarkFailed(Exception? ex)
    {
        RetryCount++;
        // Экспоненциальный backoff: 5s, 10s, 20s ... максимум 10 минут.
        var delaySeconds = Math.Min(5 * Math.Pow(2, RetryCount - 1), 600);
        NextRetryOn = DateTime.UtcNow.AddSeconds(delaySeconds);
        if (ex is not null)
        {
            var message = ex.Message ?? ex.GetType().Name;
            LastError = message.Length > 2000 ? message[..2000] : message;
        }
    }
}
