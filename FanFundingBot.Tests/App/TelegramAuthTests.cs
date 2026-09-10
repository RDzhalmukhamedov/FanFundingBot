using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using FanFundingBot.App.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.Tests.App;

[TestFixture]
public class TelegramAuthValidatorTests
{
    private const string BotToken = "test_bot_token";

    private static readonly DateTimeOffset Now =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string?> Fields(long authDate, string? hash) => new()
    {
        ["id"] = "123",
        ["first_name"] = "Test",
        ["username"] = "tester",
        ["auth_date"] = authDate.ToString(),
        ["hash"] = hash
    };

    private static string ComputeHash(Dictionary<string, string?> fields)
    {
        var checkString = string.Join("\n",
            fields.Where(kv => kv.Key != "hash" && kv.Value is not null)
                .OrderBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Key}={kv.Value}"));
        var secretKey = SHA256.HashData(Encoding.UTF8.GetBytes(BotToken));
        return Convert.ToHexString(
            HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(checkString))).ToLowerInvariant();
    }

    [Test]
    public void ValidSignature_ShouldPass()
    {
        var fields = Fields(Now.ToUnixTimeSeconds() - 100, null);
        fields["hash"] = ComputeHash(fields);

        TelegramAuthValidator.IsValid(fields, BotToken, now: Now).Should().BeTrue();
    }

    [Test]
    public void TamperedField_ShouldFail()
    {
        var fields = Fields(Now.ToUnixTimeSeconds() - 100, null);
        fields["hash"] = ComputeHash(fields);
        fields["username"] = "hacker";

        TelegramAuthValidator.IsValid(fields, BotToken, now: Now).Should().BeFalse();
    }

    [Test]
    public void Expired_ShouldFail()
    {
        var fields = Fields(Now.AddHours(-25).ToUnixTimeSeconds(), null);
        fields["hash"] = ComputeHash(fields);

        TelegramAuthValidator.IsValid(fields, BotToken, now: Now).Should().BeFalse();
    }

    [Test]
    public void MissingHash_ShouldFail()
    {
        var fields = Fields(Now.ToUnixTimeSeconds() - 100, null);

        TelegramAuthValidator.IsValid(fields, BotToken, now: Now).Should().BeFalse();
    }

    [Test]
    public void EmptyBotToken_ShouldFail()
    {
        var fields = Fields(Now.ToUnixTimeSeconds() - 100, "whatever");

        TelegramAuthValidator.IsValid(fields, string.Empty, now: Now).Should().BeFalse();
    }
}

[TestFixture]
public class JwtTokenServiceTests
{
    [Test]
    public void CreateToken_ShouldRoundtripSubject()
    {
        var secret = new string('x', 64);
        var service = new JwtTokenService(
            Options.Create(new JwtOptions { Secret = secret, ExpiresDays = 30 }));

        var token = service.CreateToken(42, "Admin");

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token,
            new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
            }, out _);

        principal.FindFirst(JwtTokenService.SubjectClaim)!.Value.Should().Be("42");
    }
}
