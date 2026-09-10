using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Domain.Entities;

public class OrderPosition
{
    public Guid OrderId { get; private set; }
    public Guid AddonId { get; private set; }
    public Addon Addon { get; private set; } = null!;

    public Quantity Quantity { get; private set; }

    private OrderPosition()
    {
    }

    public OrderPosition(Addon addon, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(addon);
        Quantity = Quantity.Create(quantity);
        Addon = addon;
        AddonId = addon.Id;
    }

    public int Plus()
    {
        Quantity = Quantity.Create(Quantity + 1);

        return Quantity;
    }

    public int Minus()
    {
        Quantity = Quantity.Create(Quantity - 1);

        return Quantity;
    }
}
