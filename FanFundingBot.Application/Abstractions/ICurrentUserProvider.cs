namespace FanFundingBot.Application.Abstractions;

public interface ICurrentUserProvider
{
    long GetCurrentUserId();
}
