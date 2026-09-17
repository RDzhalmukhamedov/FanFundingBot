namespace FanFundingBot.Domain.DomainEvents;

public sealed record OrderDeletedEvent(Guid OrderId) : DomainEvent;
