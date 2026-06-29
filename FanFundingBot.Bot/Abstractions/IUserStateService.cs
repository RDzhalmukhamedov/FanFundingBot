using System.Collections.Concurrent;
using FanFundingBot.Bot.Models;

namespace FanFundingBot.Bot.Abstractions;

public interface IUserStateService
{
    ConcurrentDictionary<long, DateTime> ActiveSessions { get; }
    UserSession GetOrCreate(long userId);
    UserSession? Get(long userId);
    void Update(long userId, UserSession session);
    void Remove(long userId);
}
