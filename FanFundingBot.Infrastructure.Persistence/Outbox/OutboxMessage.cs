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
        RetryCount++;
    }
}
