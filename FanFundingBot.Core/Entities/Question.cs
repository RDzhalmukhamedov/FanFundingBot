namespace FanFundingBot.Core.Entities;

public class Question
{
    public Guid Id { get; }
    public string Text { get; }
    public Customer User { get; }
    public DateTime CreatedAt { get; }

    public string? Answer { get; }
    public Admin? AnsweredBy { get; private set; }
    public DateTime? AnsweredAt { get; private set; }

    public Question(Customer user, string question)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        Id = Guid.NewGuid();
        User = user;
        Text = question;
        CreatedAt = DateTime.UtcNow;
    }

    public void AnswerQuestion(Admin admin, string answerText)
    {
        ArgumentNullException.ThrowIfNull(admin);
        ArgumentException.ThrowIfNullOrWhiteSpace(answerText);

        AnsweredBy = admin;
        AnsweredAt = DateTime.UtcNow;
    }
}
