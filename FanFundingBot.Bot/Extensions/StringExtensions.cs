namespace FanFundingBot.Bot.Extensions;

public static class StringExtensions
{
    public static string? NormalizePhone(string? phone)
        => Domain.Extensions.StringExtensions.NormalizePhone(phone);

    public static bool IsValidPhone(this string? phone)
        => Domain.Extensions.StringExtensions.IsValidPhone(phone);

    public static bool IsValidAddress(this string? address)
        => Domain.Extensions.StringExtensions.IsValidAddress(address);
}
