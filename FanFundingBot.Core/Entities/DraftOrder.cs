using FanFundingBot.Core.Enums;
using FanFundingBot.Core.Extensions;
using System.Xml.Linq;

namespace FanFundingBot.Core.Entities;

public class DraftOrder : Order
{
    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public DraftOrder(Customer customer, Project project) : base(customer, project)
    {
    }

    public override FormedOrder Form()
    {
        // TODO Добавить передачу других полей
        return new FormedOrder(Customer, Project);
    }

    public override DraftOrder Edit()
    {
        return this;
    }

    public override void AddAddon(Addon addon, int quantity = 1)
    {
        if (!_orderPositions.ContainsKey(addon.Id))
        {
            _orderPositions.Add(addon.Id, new OrderPosition(addon, quantity));
        }
        else
        {
            throw new Exception(); // TODOs
        }
    }

    public override void DeleteAddon(Addon addon)
    {
        if (_orderPositions.ContainsKey(addon.Id))
        {
            _orderPositions.Remove(addon.Id);
        }
        else
        {
            throw new Exception(); // TODOs
        }
    }

    public override int IncrementAddonQuantity(Guid addonId)
    {
        if (_orderPositions.ContainsKey(addonId))
        {
            return _orderPositions[addonId].Plus();
        }
        else
        {
            throw new Exception(); // TODOs
        }
    }

    public override int DecrementAddonQuantity(Guid addonId)
    {
        if (_orderPositions.ContainsKey(addonId))
        {
            return _orderPositions[addonId].Minus();
        }
        else
        {
            throw new Exception(); // TODOs
        }
    }

    public override void SetPhoneNumber(string phoneNumber)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        if (phoneNumber.IsValidPhone())
        {
            Phone = phoneNumber;
        }
    }

    public override void SetDeliveryMethod(DeliveryMethod deliveryMethod)
    {
        DeliveryMethod = deliveryMethod;
    }

    public override void SetShippingAddress(string shippingAddress)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shippingAddress);

        if (shippingAddress.IsValidPhone())
        {
            Phone = shippingAddress;
        }
    }
}
