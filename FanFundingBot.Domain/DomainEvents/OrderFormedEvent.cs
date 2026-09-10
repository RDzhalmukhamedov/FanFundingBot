namespace FanFundingBot.Domain.DomainEvents;

public sealed record OrderFormedEvent(Guid OrderId, long CustomerId, Guid ProjectId) : DomainEvent;
