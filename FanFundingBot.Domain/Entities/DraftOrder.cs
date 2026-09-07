using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Extensions;

namespace FanFundingBot.Domain.Entities;

public class DraftOrder : Order
{
    private DraftOrder()
    {
    }

    public DraftOrder(Customer customer, Project project) : base(customer, project)
    {
    }

    public override FormedOrder Form()
    {
        var formed = new FormedOrder(Customer, Project);
        formed.CopyStateFrom(this);

        return formed;
    }

    public override DraftOrder Edit()
    {
        return this;
    }

    public override void AddAddon(Addon addon, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(addon);

        if (_orderPositions.Any(p => p.Addon.Id == addon.Id))
        {
            throw new DomainException($"Дополнение «{addon.Name}» уже добавлено в заказ.");
        }

        _orderPositions.Add(new OrderPosition(addon, quantity));
    }

    public override void DeleteAddon(Addon addon)
    {
        ArgumentNullException.ThrowIfNull(addon);

        var position = _orderPositions.FirstOrDefault(p => p.Addon.Id == addon.Id);
        if (position is null)
        {
            throw new DomainException($"Дополнение «{addon.Name}» отсутствует в заказе.");
        }

        _orderPositions.Remove(position);
    }

    public override int IncrementAddonQuantity(Guid addonId)
    {
        return FindPosition(addonId).Plus();
    }

    public override int DecrementAddonQuantity(Guid addonId)
    {
        return FindPosition(addonId).Minus();
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

    public override void SetShippingAddress(string? shippingAddress)
    {
        Address = string.IsNullOrWhiteSpace(shippingAddress) ? null : shippingAddress;
    }

    private OrderPosition FindPosition(Guid addonId)
    {
        var position = _orderPositions.FirstOrDefault(p => p.Addon.Id == addonId);
        if (position is null)
        {
            throw new DomainException($"Дополнение {addonId} отсутствует в заказе.");
        }

        return position;
    }
}
