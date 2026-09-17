namespace FanFundingBot.App.Auth;

public sealed class JwtOptions
{
    public string Secret { get; set; } = string.Empty;
    public int ExpiresDays { get; set; } = 14;
}
