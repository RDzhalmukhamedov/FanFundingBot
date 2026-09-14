using System.Security.Cryptography;
using System.Text;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Проверка подписи Telegram Login Widget:
/// data_check_string из отсортированных полей, HMAC-SHA256 с ключом SHA256(bot_token).
/// Чистая функция — детерминирована через <paramref name="now"/> для тестов.
/// </summary>
public static class TelegramAuthValidator
{
    public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

    /// <summary>
    /// Допуск на расхождение часов между Telegram и сервером: auth_date на
    /// несколько секунд в будущем — норма (округление, NTP-дрейф), а не подделка.
    /// </summary>
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(5);

    public static bool IsValid(
        IReadOnlyDictionary<string, string?> fields,
        string botToken,
        TimeSpan? maxAge = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrEmpty(botToken))
            return false;

        if (!fields.TryGetValue("hash", out var hash) || string.IsNullOrEmpty(hash))
            return false;

        if (!fields.TryGetValue("auth_date", out var authDateRaw)
            || !long.TryParse(authDateRaw, out var authDate))
            return false;

        var current = now ?? DateTimeOffset.UtcNow;
        var ageSeconds = current.ToUnixTimeSeconds() - authDate;
        if (ageSeconds < -FutureTolerance.TotalSeconds || ageSeconds > (maxAge ?? DefaultMaxAge).TotalSeconds)
            return false;

        var checkString = string.Join("\n",
            fields.Where(kv => kv.Key != "hash" && kv.Value is not null)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));

        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(botToken));
        var computed = Convert.ToHexString(
            HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(checkString)));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computed),
            Encoding.UTF8.GetBytes(hash.ToUpperInvariant()));
    }
}
