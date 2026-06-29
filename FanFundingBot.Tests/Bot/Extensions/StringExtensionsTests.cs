using FanFundingBot.Bot.Extensions;

namespace FanFundingBot.Tests.Bot.Extensions;

[TestFixture]
public class StringExtensionsTests
{
    [TestCase("+71234567890")]
    [TestCase("81234567890")]
    [TestCase("+79998887766")]
    [TestCase("+71111111111")]
    public void IsValidPhone_ValidPhones_ShouldReturnTrue(string phone)
    {
        Assert.That(phone.IsValidPhone(), Is.True);
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("123")]
    [TestCase("phone")]
    [TestCase("+7123456789")]
    [TestCase("+712345678901")]
    [TestCase("abc")]
    public void IsValidPhone_InvalidPhones_ShouldReturnFalse(string? phone)
    {
        Assert.That(phone!.IsValidPhone(), Is.False);
    }

    [Test]
    public void IsValidPhone_WithSpacesAndDashes_ShouldCleanAndValidate()
    {
        var phone = "+7 123 456 78 90";
        Assert.That(phone.IsValidPhone(), Is.True);
    }

    [TestCase("г Москва, ул Ленина, д 1", true)]
    [TestCase("г Москва, ул Ленина, д 1, кв 10", true)]
    [TestCase("Москва, Ленина 1", true)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase("abc", false)]
    [TestCase("12345", false)]
    [TestCase("a 1", false)]
    public void IsValidAddress_ShouldValidateCorrectly(string? address, bool expected)
    {
        Assert.That(address!.IsValidAddress(), Is.EqualTo(expected));
    }
}
