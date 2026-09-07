using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.ValueObjects;

public readonly record struct Money
{
    private readonly decimal _value;

    private Money(decimal value) => _value = value;

    public static Money Zero => default;

    public static Money Create(decimal value)
    {
        if (value < 0)
            throw new DomainException($"Цена должна быть неотрицательной: {value}.");

        return new Money(value);
    }

    public static implicit operator decimal(Money money) => money._value;

    public static Money operator +(Money left, Money right) => new(left._value + right._value);

    public static Money operator *(Money money, int quantity) => new(money._value * quantity);

    public override string ToString() => _value.ToString("F2");
}
