using System.Text.RegularExpressions;

namespace FanFundingBot.Core.Extensions;

public static partial class StringExtensions
{
    private static readonly Regex PhoneRegex = PhoneRegexGenerated();

    public static bool IsValidPhone(this string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        return PhoneRegex.IsMatch(cleaned);
    }

    public static bool IsValidAddress(this string address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length < 5)
            return false;

        return address.Any(char.IsLetter) && address.Any(char.IsDigit);
    }

    [GeneratedRegex(@"^(\+7|8)\d{10}$")]
    private static partial Regex PhoneRegexGenerated();
}
