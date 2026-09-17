using FanFundingBot.Domain.Common;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Domain.Entities;

public class User : AggregateRoot
{
    private Dictionary<Guid, Order> _orders = [];
    private List<Question> _questions = [];

    public long Id { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string? Username { get; private set; }
    public PhoneNumber? Phone { get; private set; }
    public UserRole Role { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public IReadOnlyDictionary<Guid, Order> Orders => _orders.AsReadOnly();
    public IReadOnlyCollection<Question> Questions => _questions.AsReadOnly();

    protected User()
    {
    }

    public User(long id, string? firstName, string? lastName, string? userName)
    {
        Id = id;
        FirstName = firstName;
        LastName = lastName;
        Username = userName;
        Role = UserRole.Regular;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsAdmin() => Role == UserRole.Admin;

    public void PromoteToAdmin()
    {
        Role = UserRole.Admin;
    }

    public void SetPhone(string phone)
    {
        Phone = PhoneNumber.Create(phone);
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

    public DraftOrder StartOrdering(Project project)
    {
        ArgumentNullException.ThrowIfNull(project);

        // NB: _orders содержит только заказы текущей сессии (поле не маппится в БД).
        // Проверку дублей по базе выполняет вызывающий сервис через репозиторий.
        if (_orders.Values.Any(order => order.Project.Id == project.Id))
        {
            throw new DomainException($"Заказ на проект «{project.Name}» уже существует.");
        }

        var newOrder = new DraftOrder(this, project);
        _orders.Add(newOrder.Id, newOrder);

        return newOrder;
    }

    public DraftOrder EditOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var orderToEdit))
        {
            throw new DomainException($"Заказ {orderId} не найден.");
        }
        var editableOrder = orderToEdit.Edit();
        _orders[orderId] = editableOrder;

        return editableOrder;
    }

    public FormedOrder FormOrder(Guid orderId)
    {
        if (!_orders.TryGetValue(orderId, out var draftOrder))
        {
            throw new DomainException($"Заказ {orderId} не найден.");
        }
        var formedOrder = draftOrder.Form();
        _orders[orderId] = formedOrder;

        return formedOrder;
    }

    public void DeleteOrder(Guid orderId)
    {
        if (!_orders.ContainsKey(orderId))
        {
            throw new DomainException($"Заказ {orderId} не найден.");
        }

        _orders.Remove(orderId);
    }
}
