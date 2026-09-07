using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.DomainEvents;

public sealed record ProjectMemberAddedEvent(Guid ProjectId, long UserId, ProjectRole Role) : DomainEvent;
