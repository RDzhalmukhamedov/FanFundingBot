namespace FanFundingBot.App.Auth;

/// <summary>
/// Stateless-продление сессии без refresh-таблицы: если до истечения JWT
/// осталось меньше половины срока жизни — перевыпустить.
/// Чистая функция, покрыта unit-тестами.
/// </summary>
public static class SlidingRefreshPolicy
{
    public static bool ShouldReissue(DateTimeOffset expires, DateTimeOffset now, int expiresDays)
    {
        if (expiresDays <= 0)
            return false;

        return expires - now < TimeSpan.FromDays(expiresDays * 0.5);
    }
}
