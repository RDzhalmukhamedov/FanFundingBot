using System.Text.RegularExpressions;

namespace FanFundingBot.Domain.Extensions;

public static partial class StringExtensions
{
    private static readonly Regex PhoneRegex = PhoneRegexGenerated();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var cleaned = phone.Replace(" ", "").Replace("\t", "")
            .Replace("-", "").Replace("(", "").Replace(")", "").Replace(".", "");

        if (cleaned.Length == 11 && cleaned[0] == '8' && cleaned.All(char.IsDigit))
            cleaned = "+7" + cleaned[1..];

        return cleaned;
    }

    public static bool IsValidPhone(this string? phone)
    {
        var cleaned = NormalizePhone(phone);
        return cleaned is not null && PhoneRegex.IsMatch(cleaned);
    }

    public static bool IsValidAddress(this string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length < 5)
            return false;

        return address.Any(char.IsLetter) && address.Any(char.IsDigit);
    }

    [GeneratedRegex(@"^\+\d{8,15}$")]
    private static partial Regex PhoneRegexGenerated();
}
