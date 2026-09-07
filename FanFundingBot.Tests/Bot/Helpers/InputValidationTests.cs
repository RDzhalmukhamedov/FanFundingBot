using FanFundingBot.Bot.Helpers;
using FluentAssertions;
using NUnit.Framework;

namespace FanFundingBot.Tests.Bot.Helpers;

[TestFixture]
public class InputValidationTests
{
    [TestCase("+71234567890", null)]
    [TestCase("81234567890", null)]
    [TestCase("", "Пожалуйста, введите номер телефона.")]
    [TestCase(null, "Пожалуйста, введите номер телефона.")]
    [TestCase("123", "Пожалуйста, введите корректный номер телефона в формате +7XXXXXXXXXX или 8XXXXXXXXXX.")]
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
}
