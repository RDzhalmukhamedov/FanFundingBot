using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Telegram.Bot;

namespace FanFundingBot.Bot.Services;

public class SessionExpirationService : BackgroundService
{
    private readonly IUserStateService _stateService;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<SessionExpirationService> _logger;
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);

    public SessionExpirationService(
        IUserStateService stateService,
        ITelegramBotClient botClient,
        BotConfiguration config,
        ILogger<SessionExpirationService> logger)
    {
        _stateService = stateService;
        _botClient = botClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SessionExpirationService started (check interval: {Interval}s)", CheckInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);
            await CheckExpiredSessionsAsync(stoppingToken);
        }
    }

    private async Task CheckExpiredSessionsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var expired = _stateService.ActiveSessions
            .Where(kvp => kvp.Value <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        if (expired.Count == 0)
            return;

        foreach (var userId in expired)
        {
            try
            {
                _stateService.Remove(userId);

                await _botClient.SendMessage(
                    chatId: userId,
                    text: "⏰ Время сессии истекло. Оформление заказа отменено.\n\n" +
                          "Если хотите оформить заказ заново, нажмите «🛒 Оформить заказ».",
                    cancellationToken: ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify user {UserId} about session expiration", userId);
            }
        }
    }
}