using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public abstract class User
{
    protected Dictionary<Guid, Order> _orders = [];
    protected List<Question> _questions = [];

    public long Id { get; }
    public string? FirstName { get; }
    public string? LastName { get; }
    public string? Username { get; }
    public string? Phone { get; private set; }
    public virtual UserRole Role { get; }

    public DateTime CreatedAt { get; }

    public IReadOnlyDictionary<Guid, Order> Orders => _orders.AsReadOnly();
    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    protected User(string firstName, string lastName, string userName)
    {
        FirstName = firstName;
        LastName = lastName;
        Username = userName;
    }

    // TODO Надо переопрерделить в Админе и убедиться что будет нормально работать даже если передавать не Админа а Кастомера
    public virtual bool IsAdmin() => Role == UserRole.Admin;

    public void CreateOrder(Order order)
    {
        _orders.Add(order.Id, order);
    }

    public void AskQuestion(Question question)
    {
        _questions.Add(question);
    }
}
