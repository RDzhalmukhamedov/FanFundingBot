using FanFundingBot.Domain.DomainEvents;
using FanFundingBot.Domain.Enums;
using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.ValueObjects;

namespace FanFundingBot.Domain.Entities;

public class DraftOrder : Order
{
    private DraftOrder()
    {
    }

    public DraftOrder(User customer, Project project) : base(customer, project)
    {
    }

    public override FormedOrder Form()
    {
        var formed = new FormedOrder(User, Project);
        formed.CopyStateFrom(this);
        formed.AddDomainEvent(new OrderFormedEvent(formed.Id, formed.User.Id, formed.Project.Id));

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
        Phone = PhoneNumber.Create(phoneNumber);
    }

    public override void SetDeliveryMethod(DeliveryMethod deliveryMethod)
    {
        DeliveryMethod = deliveryMethod;
    }

    public override void SetShippingAddress(string? shippingAddress)
    {
        Address = string.IsNullOrWhiteSpace(shippingAddress) ? null : ValueObjects.Address.Create(shippingAddress);
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
