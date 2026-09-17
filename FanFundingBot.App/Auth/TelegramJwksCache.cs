using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.App.Auth;

/// <summary>
/// Кэш JWKS Telegram (https://oauth.telegram.org/.well-known/jwks.json), 24ч.
/// Ошибка сети — обычный неуспех (null + warn), endpoint вернёт 401, не 500.
/// </summary>
public sealed class TelegramJwksCache
{
    public const string JwksUrl = "https://oauth.telegram.org/.well-known/jwks.json";

    private static readonly TimeSpan CacheLifetime = TimeSpan.FromHours(24);

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<TelegramJwksCache> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private JsonWebKeySet? _cached;
    private DateTimeOffset _expiresAt = DateTimeOffset.MinValue;

    public TelegramJwksCache(IHttpClientFactory httpFactory, ILogger<TelegramJwksCache> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<JsonWebKeySet?> GetKeysAsync(CancellationToken ct)
    {
        if (_cached is not null && DateTimeOffset.UtcNow < _expiresAt)
            return _cached;

        await _lock.WaitAsync(ct);
        try
        {
            if (_cached is not null && DateTimeOffset.UtcNow < _expiresAt)
                return _cached;

            var client = _httpFactory.CreateClient();
            using var response = await client.GetAsync(JwksUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telegram JWKS: HTTP {Status}", (int)response.StatusCode);
                return _cached; // stale лучше, чем ничего
            }

            var json = await response.Content.ReadAsStringAsync(ct);
            var keys = new JsonWebKeySet(json);
            if (keys.Keys.Count == 0)
            {
                _logger.LogWarning("Telegram JWKS: пустой набор ключей");
                return _cached;
            }

            _cached = keys;
            _expiresAt = DateTimeOffset.UtcNow.Add(CacheLifetime);
            return _cached;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Telegram JWKS: не удалось загрузить ключи");
            return _cached;
        }
        finally
        {
            _lock.Release();
        }
    }
}
