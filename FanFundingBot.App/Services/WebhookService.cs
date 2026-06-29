using FanFundingBot.Bot.Models;
using Telegram.Bot;

namespace FanFundingBot.App.Services;

public class WebhookService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _botConfig;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        ITelegramBotClient botClient,
        BotConfiguration botConfig,
        ILogger<WebhookService> logger)
    {
        _botClient = botClient;
        _botConfig = botConfig;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var webhookUrl = $"{_botConfig.HostAddress.TrimEnd('/')}{_botConfig.Route}";

        _logger.LogInformation("Setting webhook to: {WebhookUrl}", webhookUrl);

        await _botClient.SetWebhook(
            url: webhookUrl,
            secretToken: _botConfig.SecretToken,
            cancellationToken: stoppingToken);
    }
}
