using FanFundingBot.Core.Enums;

namespace FanFundingBot.Core.Entities;

public abstract class Order
{
    protected Dictionary<Guid, OrderPosition> _orderPositions = [];

    public Guid Id { get; }

    public Customer Customer { get; }
    public Project Project { get; }

    public string Phone { get; protected set; } = string.Empty;
    public DeliveryMethod DeliveryMethod { get; protected set; }
    public string? Address { get; protected set; }
    public OrderStatus Status { get; protected set; }

    public IReadOnlyDictionary<Guid, OrderPosition> OrderAddons => _orderPositions.AsReadOnly();

    public Order(Customer customer, Project project)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(project);

        Id = Guid.NewGuid();
        Customer = customer;
        Project = project;

    }

    public abstract FormedOrder Form();
    public abstract DraftOrder Edit();
    public abstract void AddAddon(Addon addon, int quantity = 1);
    public abstract void DeleteAddon(Addon addon);
    public abstract int IncrementAddonQuantity(Guid addonId);
    public abstract int DecrementAddonQuantity(Guid addonId);
    public abstract void SetPhoneNumber(string phoneNumber);
    public abstract void SetDeliveryMethod(DeliveryMethod deliveryMethod);
    public abstract void SetShippingAddress(string shippingAddress);
}
