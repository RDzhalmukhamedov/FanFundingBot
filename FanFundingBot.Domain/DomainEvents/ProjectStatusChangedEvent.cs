namespace FanFundingBot.Domain.DomainEvents;

public sealed record ProjectStatusChangedEvent(Guid ProjectId, int StatusId, string StatusName) : DomainEvent;
