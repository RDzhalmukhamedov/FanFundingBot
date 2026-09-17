namespace FanFundingBot.App.Auth;

/// <summary>
/// Решение ревалидации circuit-сессии по свежим данным БД.
/// Чистая функция, покрыта unit-тестами; вызывается из
/// <see cref="CookieAuthenticationStateProvider"/> каждые 30 минут.
/// </summary>
public static class SessionRevalidation
{
    public enum Verdict
    {
        Valid,
        RefreshRole,
        Invalid
    }

    public static Verdict Check(UserSession? session, long? dbUserId, string? dbRole, DateTimeOffset now)
    {
        if (session is null || session.UserId <= 0 || session.Expires <= now)
            return Verdict.Invalid;

        if (dbUserId is null || dbUserId <= 0)
            return Verdict.Invalid;

        if (!string.Equals(dbRole, session.Role, StringComparison.Ordinal))
            return Verdict.RefreshRole;

        return Verdict.Valid;
    }
}
