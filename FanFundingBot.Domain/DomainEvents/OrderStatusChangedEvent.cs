using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.DomainEvents;

public sealed record OrderStatusChangedEvent(Guid OrderId, OrderStatus OldStatus, OrderStatus NewStatus) : DomainEvent;
