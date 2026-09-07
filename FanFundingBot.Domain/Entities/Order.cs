using FanFundingBot.Domain.Enums;

namespace FanFundingBot.Domain.Entities;

public abstract class Order
{
    protected List<OrderPosition> _orderPositions = [];

    public Guid Id { get; private set; }

    public Customer Customer { get; private set; } = null!;
    public Project Project { get; private set; } = null!;

    public DateTime CreatedAt { get; protected set; }

    public string Phone { get; protected set; } = string.Empty;
    public DeliveryMethod DeliveryMethod { get; protected set; }
    public string? Address { get; protected set; }
    public OrderStatus Status { get; protected set; }

    public IReadOnlyList<OrderPosition> OrderAddons => _orderPositions.AsReadOnly();

    protected Order()
    {
    }

    public Order(Customer customer, Project project)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(project);

        Id = Guid.NewGuid();
        Customer = customer;
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
}
