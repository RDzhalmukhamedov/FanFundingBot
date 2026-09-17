using FanFundingBot.Domain.Extensions;
using FanFundingBot.Domain.Exceptions;

namespace FanFundingBot.Domain.ValueObjects;

public readonly record struct PhoneNumber
{
    private readonly string? _value;

    private PhoneNumber(string value) => _value = value;

    public static PhoneNumber Empty => default;

    public bool IsEmpty => string.IsNullOrEmpty(_value);

    public static PhoneNumber Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Номер телефона не может быть пустым.");

        var normalized = StringExtensions.NormalizePhone(value);
        if (normalized is null || !normalized.IsValidPhone())
            throw new DomainException($"Некорректный номер телефона: {value}.");

        return new PhoneNumber(normalized);
    }

    public override string ToString() => _value ?? string.Empty;

    public static implicit operator string(PhoneNumber phone) => phone.ToString();
}
