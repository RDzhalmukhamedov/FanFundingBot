using System.Globalization;
using System.Text;
using FanFundingBot.Domain.Extensions;

namespace FanFundingBot.Bot.Helpers;

public static class InputValidation
{
    public static string? ValidatePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return "Пожалуйста, введите номер телефона.";

        if (!phone.IsValidPhone())
            return "Пожалуйста, введите корректный номер телефона в формате +XXXXXXXXXXX (8–15 цифр).";

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

    public static string? ValidatePrice(string? text, out decimal price)
    {
        price = 0;

        if (!TryParsePrice(text, out price) || price < 0)
            return "Некорректная цена. Введите число (например: 499.99):";

        return null;
    }

    // Точка и запятая принимаются при любой серверной культуре:
    // десятичный разделитель — последний из них, остальные/пробелы — группировки.
    private static bool TryParsePrice(string? text, out decimal price)
    {
        price = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var s = text.Trim().Replace(" ", "").Replace("\u00A0", "");
        if (s.Length == 0)
            return false;

        var lastSep = Math.Max(s.LastIndexOf('.'), s.LastIndexOf(','));
        string normalized;
        if (lastSep < 0)
        {
            normalized = s;
        }
        else
        {
            var sb = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (c == '.' || c == ',')
                {
                    if (i == lastSep)
                        sb.Append('.');
                }
                else
                {
                    sb.Append(c);
                }
            }

            normalized = sb.ToString();
        }

        return decimal.TryParse(normalized, NumberStyles.Number, CultureInfo.InvariantCulture, out price);
    }
}
