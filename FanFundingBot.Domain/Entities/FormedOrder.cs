using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.Entities;

public class FormedOrder : Order
{
    private FormedOrder()
    {
    }

    public FormedOrder(User customer, Project project) : base(customer, project)
    {
    }

    public override FormedOrder Form()
    {
        throw new OrderNotEditModeException();
    }

    public override DraftOrder Edit()
    {
        var draft = new DraftOrder(User, Project);
        draft.CopyStateFrom(this);

        return draft;
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

    public override void SetShippingAddress(string? shippingAddress)
    {
        throw new OrderNotEditModeException();
    }
}
