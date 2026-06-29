namespace FanFundingBot.Bot.Models;

public class BotConfiguration
{
    public string BotToken { get; set; } = string.Empty;
    public string HostAddress { get; set; } = string.Empty;
    public string Route { get; set; } = "/api/bot";
    public long OwnerId { get; set; }
    public string SecretToken { get; set; } = string.Empty;
    public bool UsePolling { get; set; }
    public int SendDelayMs { get; set; } = 50;
}
