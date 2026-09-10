using FanFundingBot.Domain.Exceptions;
using FanFundingBot.Domain.Extensions;

namespace FanFundingBot.Domain.ValueObjects;

public readonly record struct Address
{
    private readonly string? _value;

    private Address(string value) => _value = value;

    public static Address Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Адрес не может быть пустым.");

        if (!value.IsValidAddress())
            throw new DomainException("Некорректный адрес доставки.");

        return new Address(value.Trim());
    }

    public override string ToString() => _value ?? string.Empty;

    public static implicit operator string(Address address) => address.ToString();
}
