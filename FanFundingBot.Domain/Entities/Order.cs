using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Domain.Entities;

public abstract class Order : AggregateRoot
{
    protected List<OrderPosition> _orderPositions = [];

    public Guid Id { get; private set; }

    public User User { get; private set; } = null!;
    public Project Project { get; private set; } = null!;

    public DateTime CreatedAt { get; protected set; }

    public PhoneNumber Phone { get; protected set; }
    public DeliveryMethod DeliveryMethod { get; protected set; }
    public Address? Address { get; protected set; }
    public OrderStatus Status { get; protected set; }

    public IReadOnlyList<OrderPosition> OrderAddons => _orderPositions.AsReadOnly();

    protected Order()
    {
    }

    public Order(User customer, Project project)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(project);

        Id = Guid.NewGuid();
        User = customer;
        Project = project;
        CreatedAt = DateTime.UtcNow;
        Status = OrderStatus.New;
    }

    internal void CopyStateFrom(Order source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Id = source.Id;
        CreatedAt = source.CreatedAt;
        Phone = source.Phone;
        DeliveryMethod = source.DeliveryMethod;
        Address = source.Address;
        Status = source.Status;

        _orderPositions.Clear();
        foreach (var position in source._orderPositions)
        {
            _orderPositions.Add(new OrderPosition(position.Addon, position.Quantity));
        }
    }

    public abstract FormedOrder Form();
    public abstract DraftOrder Edit();
    public abstract void AddAddon(Addon addon, int quantity = 1);
    public abstract void DeleteAddon(Addon addon);
    public abstract int IncrementAddonQuantity(Guid addonId);
    public abstract int DecrementAddonQuantity(Guid addonId);
    public abstract void SetPhoneNumber(string phoneNumber);
    public abstract void SetDeliveryMethod(DeliveryMethod deliveryMethod);
    public abstract void SetShippingAddress(string? shippingAddress);

    public void SetStatus(OrderStatus newStatus)
    {
        if (Status == newStatus)
            return;

        var oldStatus = Status;
        Status = newStatus;

        AddDomainEvent(new OrderStatusChangedEvent(Id, oldStatus, newStatus));
    }
}
