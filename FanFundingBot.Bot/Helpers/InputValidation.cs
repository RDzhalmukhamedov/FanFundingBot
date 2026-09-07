using FanFundingBot.Domain.Extensions;

namespace FanFundingBot.Bot.Helpers;

public static class InputValidation
{
    public static string? ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Пожалуйста, введите номер телефона.";

        var cleaned = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (!cleaned.IsValidPhone())
            return "Пожалуйста, введите корректный номер телефона в формате +7XXXXXXXXXX или 8XXXXXXXXXX.";

        return null;
    }

    public static string? ValidateAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return "Пожалуйста, введите адрес доставки.";

        if (!address.IsValidAddress())
            return "Адрес должен содержать не менее 5 символов, включая буквы и цифры.";

        return null;
    }
}
