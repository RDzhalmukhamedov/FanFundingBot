using System.Collections.Concurrent;
using FanFundingBot.Bot.Abstractions;
using FanFundingBot.Bot.Models;
using Microsoft.Extensions.Caching.Memory;

namespace FanFundingBot.Bot.Services;

public class UserStateService : IUserStateService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);

    public ConcurrentDictionary<long, DateTime> ActiveSessions { get; } = new();

    public UserStateService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public UserSession GetOrCreate(long userId)
    {
        var key = GetKey(userId);
        if (_cache.Get<UserSession>(key) is { } session)
        {
            ActiveSessions[userId] = DateTime.UtcNow + SessionTtl;
            return session;
        }

        session = new UserSession();
        _cache.Set(key, session, SessionTtl);
        ActiveSessions[userId] = DateTime.UtcNow + SessionTtl;
        return session;
    }

    public UserSession? Get(long userId)
    {
        var session = _cache.Get<UserSession>(GetKey(userId));
        if (session != null)
            ActiveSessions[userId] = DateTime.UtcNow + SessionTtl;
        return session;
    }

    public void Update(long userId, UserSession session)
    {
        _cache.Set(GetKey(userId), session, SessionTtl);
        ActiveSessions[userId] = DateTime.UtcNow + SessionTtl;
    }

    public void Remove(long userId)
    {
        _cache.Remove(GetKey(userId));
        ActiveSessions.TryRemove(userId, out _);
    }

    private static string GetKey(long userId) => $"user_session_{userId}";
}
