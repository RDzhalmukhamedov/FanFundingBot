using FanFundingBot.App.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FanFundingBot.Tests.App;

[TestFixture]
public class SlidingRefreshPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    [TestCase(29, false)]
    [TestCase(16, false)]
    [TestCase(14, true)]
    [TestCase(0, true)]
    [TestCase(-1, true)]
    public void ShouldReissue_HalfWindow(int daysLeft, bool expected)
    {
        SlidingRefreshPolicy.ShouldReissue(Now.AddDays(daysLeft), Now, 30).Should().Be(expected);
    }

    [Test]
    public void ShouldReissue_NonPositiveExpiresDays_ReturnsFalse()
    {
        SlidingRefreshPolicy.ShouldReissue(Now, Now, 0).Should().BeFalse();
    }
}

[TestFixture]
public class AuthCookieWriterTests
{
    [Test]
    public void Append_SetsHttpOnlyLaxCookie()
    {
        var ctx = new DefaultHttpContext();

        AuthCookieWriter.Append(ctx.Response, "tok123", 14, isHttps: false);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        setCookie.Should().ContainEquivalentOf("ffbot_auth=tok123");
        setCookie.Should().ContainEquivalentOf("httponly");
        setCookie.Should().ContainEquivalentOf("samesite=lax");
        setCookie.Should().ContainEquivalentOf("path=/");
    }

    [Test]
    public void Clear_MirrorsAppendPath()
    {
        var ctx = new DefaultHttpContext();

        AuthCookieWriter.Clear(ctx.Response, isHttps: false);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        setCookie.Should().ContainEquivalentOf("ffbot_auth=");
        setCookie.Should().ContainEquivalentOf("expires=");
        setCookie.Should().ContainEquivalentOf("path=/");
    }
}

[TestFixture]
public class SessionRevalidationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

    private static UserSession Session() => new()
    {
        UserId = 7,
        Role = "Admin",
        Expires = Now.AddDays(1)
    };

    [Test]
    public void Check_NullSession_Invalid()
    {
        SessionRevalidation.Check(null, 7, "Admin", Now)
            .Should().Be(SessionRevalidation.Verdict.Invalid);
    }

    [Test]
    public void Check_ExpiredSession_Invalid()
    {
        var expired = Session();
        expired.Expires = Now.AddMinutes(-1);

        SessionRevalidation.Check(expired, 7, "Admin", Now)
            .Should().Be(SessionRevalidation.Verdict.Invalid);
    }

    [Test]
    public void Check_MissingUser_Invalid()
    {
        SessionRevalidation.Check(Session(), null, null, Now)
            .Should().Be(SessionRevalidation.Verdict.Invalid);
    }

    [Test]
    public void Check_RoleChanged_RefreshRole()
    {
        SessionRevalidation.Check(Session(), 7, "User", Now)
            .Should().Be(SessionRevalidation.Verdict.RefreshRole);
    }

    [Test]
    public void Check_FreshSession_Valid()
    {
        SessionRevalidation.Check(Session(), 7, "Admin", Now)
            .Should().Be(SessionRevalidation.Verdict.Valid);
    }
}
