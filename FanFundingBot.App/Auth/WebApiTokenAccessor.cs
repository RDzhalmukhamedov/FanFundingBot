namespace FanFundingBot.App.Auth;

/// <summary>
/// Токен веб-API текущего circuit. Заполняется провайдером состояния
/// аутентификации (из cookie при prerender, из persisted state в интерактиве)
/// и подставляется в заголовок Authorization исходящих вызовов к своему API.
/// </summary>
public sealed class WebApiTokenAccessor
{
    public string? Token { get; set; }
}
