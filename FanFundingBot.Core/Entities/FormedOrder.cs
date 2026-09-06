using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Exceptions;

namespace FanFundingBot.Core.Entities;

public class FormedOrder : Order
{
    public DateTime CreatedAt { get; } = DateTime.UtcNow;

    public FormedOrder(Customer customer, Project project) : base(customer, project)
    {
    }

    public override FormedOrder Form()
    {
        throw new OrderNotEditModeException();
    }

    public override DraftOrder Edit()
    {
        // TODO Добавить передачу других полей
        return new DraftOrder(Customer, Project);
    }

    public override void AddAddon(Addon addon, int quantity = 1)
    {
        throw new OrderNotEditModeException();
    }

    public override void DeleteAddon(Addon addon)
    {
        throw new OrderNotEditModeException();
    }

    public override int IncrementAddonQuantity(Guid addonId)
    {
        throw new OrderNotEditModeException();
    }

    public override int DecrementAddonQuantity(Guid addonId)
    {
        throw new OrderNotEditModeException();
    }

    public override void SetPhoneNumber(string phoneNumber)
    {
        throw new OrderNotEditModeException();
    }

    public override void SetDeliveryMethod(DeliveryMethod deliveryMethod)
    {
        throw new OrderNotEditModeException();
    }

    public override void SetShippingAddress(string shippingAddress)
    {
        throw new OrderNotEditModeException();
    }
}
