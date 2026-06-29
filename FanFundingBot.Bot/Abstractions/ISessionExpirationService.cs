namespace FanFundingBot.Bot.Abstractions;

public interface ISessionExpirationService
{
    Task ExecuteAsync(CancellationToken stoppingToken);
}
