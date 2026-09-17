using FanFundingBot.Bot.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace FanFundingBot.Tests.Bot.Helpers;

[TestFixture]
public class InputValidationTests
{
    [TestCase("+71234567890", null)]
    [TestCase("81234567890", null)]
    [TestCase("+375291234567", null)]
    [TestCase("+380671234567", null)]
    [TestCase("", "Пожалуйста, введите номер телефона.")]
    [TestCase(null, "Пожалуйста, введите номер телефона.")]
    [TestCase("123", "Пожалуйста, введите корректный номер телефона в формате +XXXXXXXXXXX (8–15 цифр).")]
    public void ValidatePhone_ShouldMatchExpected(string? input, string? expected)
    {
        InputValidation.ValidatePhone(input).Should().Be(expected);
    }

    [TestCase("г Москва, ул Ленина, д 1", null)]
    [TestCase("", "Пожалуйста, введите адрес доставки.")]
    [TestCase(null, "Пожалуйста, введите адрес доставки.")]
    [TestCase("ab", "Адрес должен содержать не менее 5 символов, включая буквы и цифры.")]
    public void ValidateAddress_ShouldMatchExpected(string? input, string? expected)
    {
        InputValidation.ValidateAddress(input).Should().Be(expected);
    }

    [TestCase("499.99", null, 499.99)]
    [TestCase("0", null, 0)]
    [TestCase("499,99", null, 499.99)]
    [TestCase("", "Некорректная цена. Введите число (например: 499.99):", 0)]
    [TestCase(null, "Некорректная цена. Введите число (например: 499.99):", 0)]
    [TestCase("abc", "Некорректная цена. Введите число (например: 499.99):", 0)]
    [TestCase("-5", "Некорректная цена. Введите число (например: 499.99):", 0)]
    public void ValidatePrice_ShouldMatchExpected(string? input, string? expectedError, decimal expectedPrice)
    {
        var error = InputValidation.ValidatePrice(input, out var price);

        error.Should().Be(expectedError);
        if (expectedError is null)
            price.Should().Be(expectedPrice);
    }
}
