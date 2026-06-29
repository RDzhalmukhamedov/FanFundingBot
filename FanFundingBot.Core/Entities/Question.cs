namespace FanFundingBot.Core.Entities;

public class Question
{
    public Guid Id { get; set; }
    public long UserId { get; set; }
    public string Text { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public long? AnsweredByUserId { get; set; }

    public User User { get; set; } = null!;
    public User? AnsweredByUser { get; set; }
}
