namespace FanFundingBot.App.Auth;

/// <summary>
/// Пользователь, извлечённый из проверенного Telegram OIDC id_token.
/// </summary>
public sealed record TelegramOidcUser(
    long Id,
    string? FirstName,
    string? LastName,
    string? Username,
    string? PhotoUrl);
