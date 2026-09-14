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

        AuthCookieWriter.Append(ctx.Response, "tok123", 30, isHttps: false);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        setCookie.Should().ContainEquivalentOf("ffbot_auth=tok123");
        setCookie.Should().ContainEquivalentOf("httponly");
        setCookie.Should().ContainEquivalentOf("samesite=lax");
    }

    [Test]
    public void Clear_ExpiresCookie()
    {
        var ctx = new DefaultHttpContext();

        AuthCookieWriter.Clear(ctx.Response);

        var setCookie = ctx.Response.Headers.SetCookie.ToString();
        setCookie.Should().ContainEquivalentOf("ffbot_auth=");
        setCookie.Should().ContainEquivalentOf("expires=");
    }
}
