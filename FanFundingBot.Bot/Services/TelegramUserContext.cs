using FanFundingBot.Application.Abstractions;

namespace FanFundingBot.Bot.Services;

public class TelegramUserContext
{
    public long UserId { get; set; }
}

public class TelegramCurrentUserProvider : ICurrentUserProvider
{
    private readonly TelegramUserContext _context;

    public TelegramCurrentUserProvider(TelegramUserContext context)
    {
        _context = context;
    }

    public long GetCurrentUserId() => _context.UserId;
}
