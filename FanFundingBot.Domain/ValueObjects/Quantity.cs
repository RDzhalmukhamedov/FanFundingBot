using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.ValueObjects;

public readonly record struct Quantity
{
    private readonly int _value;

    private Quantity(int value) => _value = value;

    public static Quantity Create(int value)
    {
        if (value < 1 || value > 100)
            throw new DomainException($"Количество должно быть от 1 до 100: {value}.");

        return new Quantity(value);
    }

    public static implicit operator int(Quantity quantity) => quantity._value;
}
