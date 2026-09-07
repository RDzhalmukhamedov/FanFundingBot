namespace FanFundingBot.Domain.Entities;

public class OrderPosition
{
    public Guid OrderId { get; private set; }
    public Guid AddonId { get; private set; }
    public Addon Addon { get; private set; } = null!;

    public int Quantity { get; private set; }

    private OrderPosition()
    {
    }

    public OrderPosition(Addon addon, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(addon);
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "���������� ������ ���� �������������"); // TODO �������� ������� Extension
        }
        Quantity = quantity;
        Addon = addon;
        AddonId = addon.Id;
    }

    public int Plus()
    {
        var newQuantity = Quantity + 1;
        if (newQuantity > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "���������� �� ����� ���� ������ 100"); // TODO �������� ������� Extension ��� ���� ��� ������
        }
        Quantity = newQuantity;

        return Quantity;
    }

    public int Minus()
    {
        var newQuantity = Quantity - 1;
        if (newQuantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "���������� ������ ���� �������������"); // TODO �������� ������� Extension
        }
        Quantity = newQuantity;

        return Quantity;
    }
}
