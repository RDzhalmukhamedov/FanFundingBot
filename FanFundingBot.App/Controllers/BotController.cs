using FanFundingBot.Bot.Handlers;
using FanFundingBot.Bot.Models;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace FanFundingBot.App.Controllers;

[ApiController]
[Route("{controller}/{action}")]
public class BotController : ControllerBase
{
    private readonly ITelegramBotClient _botClient;
    private readonly UpdateHandler _updateHandler;
    private readonly BotConfiguration _botConfig;

    public BotController(
        ITelegramBotClient botClient,
        UpdateHandler updateHandler,
        BotConfiguration botConfig)
    {
        _botClient = botClient;
        _updateHandler = updateHandler;
        _botConfig = botConfig;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] Update update,
        [FromHeader(Name = "X-Telegram-Bot-Api-Secret-Token")] string? secretToken,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(_botConfig.SecretToken)
            && secretToken != _botConfig.SecretToken)
        {
            return Unauthorized();
        }

        if (update == null)
            return Ok();

        await _updateHandler.HandleUpdateAsync(_botClient, update, ct);

        return Ok();
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok("Bot webhook is running.");
    }
}
