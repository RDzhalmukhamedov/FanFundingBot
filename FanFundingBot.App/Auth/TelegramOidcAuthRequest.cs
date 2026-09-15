namespace FanFundingBot.App.Auth;

/// <summary>
/// Запрос входа через новую Telegram Login library (telegram-login.js):
/// клиент присылает OIDC id_token, сервер проверяет подпись по JWKS.
/// </summary>
public sealed record TelegramOidcAuthRequest(string? IdToken);
