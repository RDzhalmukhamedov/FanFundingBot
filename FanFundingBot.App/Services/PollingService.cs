using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace FanFundingBot.App.Services;

public class PollingService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PollingService> _logger;

    public PollingService(
        ITelegramBotClient botClient,
        IServiceScopeFactory scopeFactory,
        ILogger<PollingService> logger)
    {
        _botClient = botClient;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting polling service");

        await _botClient.DeleteWebhook(cancellationToken: stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = [],
            DropPendingUpdates = true
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var updateHandler = scope.ServiceProvider.GetRequiredService<FanFundingBot.Bot.Abstractions.IUpdateHandler>();
            await updateHandler.HandleUpdateAsync(botClient, update, ct);
        }
        catch (Exception ex)
        {
            var chatId = update.Message?.Chat?.Id
                ?? update.CallbackQuery?.Message?.Chat?.Id;
            _logger.LogError(ex, "Error handling update {UpdateId} (type: {UpdateType}, chat: {ChatId})",
                update.Id, update.Type, chatId);
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken ct)
    {
        _logger.LogError(exception, "Polling infrastructure error");
        return Task.CompletedTask;
    }
}
