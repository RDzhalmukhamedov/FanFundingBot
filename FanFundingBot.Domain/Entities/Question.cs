using FanFundingBot.Domain.Common;
using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.Entities;

public class Question : AggregateRoot
{
    public Guid Id { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public User User { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }

    public string? Answer { get; private set; }
    public User? AnsweredBy { get; private set; }
    public DateTime? AnsweredAt { get; private set; }

    private Question()
    {
    }

    public Question(User user, string question)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        Id = Guid.NewGuid();
        User = user;
        Text = question;
        CreatedAt = DateTime.UtcNow;
    }

    public void AnswerQuestion(User admin, string answerText)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerText);

        if (!admin.IsAdmin())
        {
            throw new DomainException("Отвечать на вопросы может только администратор.");
        }

        Answer = answerText;
        AnsweredBy = admin;
        AnsweredAt = DateTime.UtcNow;

        AddDomainEvent(new QuestionAnsweredEvent(Id, admin.Id));
    }
}
