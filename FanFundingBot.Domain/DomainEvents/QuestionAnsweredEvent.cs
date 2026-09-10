namespace FanFundingBot.Domain.DomainEvents;

public sealed record QuestionAnsweredEvent(Guid QuestionId, long AnsweredByUserId) : DomainEvent;
