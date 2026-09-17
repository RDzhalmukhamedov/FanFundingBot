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

        var trimmed = value.Trim();

        if (trimmed.Length > 256)
            throw new DomainException("Адрес слишком длинный (макс. 256).", "address.too_long");

        if (!trimmed.IsValidAddress())
            throw new DomainException("Некорректный адрес доставки.");

        return new Address(trimmed);
    }

    public override string ToString() => _value ?? string.Empty;

    public static implicit operator string(Address address) => address.ToString();
}
