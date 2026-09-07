using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.Entities;

public abstract class User
{
    protected Dictionary<Guid, Order> _orders = [];
    protected List<Question> _questions = [];

    public long Id { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Username { get; private set; }
    public string? Phone { get; private set; }
    public virtual UserRole Role { get; protected set; }

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyDictionary<Guid, Order> Orders => _orders.AsReadOnly();
    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    protected User()
    {
    }

    protected User(long id, string? firstName, string? lastName, string? userName)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Username = userName;
        CreatedAt = DateTime.UtcNow;
    }

    public virtual bool IsAdmin() => Role == UserRole.Admin;

    public void PromoteToAdmin()
    {
        Role = UserRole.Admin;
    }

    public void SetPhone(string phone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phone);
        Phone = phone;
    }

    public void CreateOrder(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        _orders.Add(order.Id, order);
    }

    public void AskQuestion(Question question)
    {
        ArgumentNullException.ThrowIfNull(question);
        _questions.Add(question);
    }
}
