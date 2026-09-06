namespace FanFundingBot.Core.Entities;

public class OrderPosition
{
    public Addon Addon { get; }

    public int Quantity { get; private set; }

    public OrderPosition(Addon addon, int quantity = 1)
    {
        ArgumentNullException.ThrowIfNull(addon);
        if (quantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Количество должно быть положительным"); // TODO Возможно сделать Extension
        }
        Quantity = quantity;
        Addon = addon;
    }

    public int Plus()
    {
        var newQuantity = Quantity + 1;
        if (newQuantity > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "Количество не может быть больше 100"); // TODO Возможно сделать Extension или свой тип ошибки
        }
        Quantity = newQuantity;

        return Quantity;
    }

    public int Minus()
    {
        var newQuantity = Quantity - 1;
        if (newQuantity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(Quantity), Quantity, "Количество должно быть положительным"); // TODO Возможно сделать Extension
        }
        Quantity = newQuantity;

        return Quantity;
    }
}
