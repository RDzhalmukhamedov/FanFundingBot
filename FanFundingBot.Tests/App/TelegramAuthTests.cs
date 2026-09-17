using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using FanFundingBot.App.Auth;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.Tests.App;

[TestFixture]
public class TelegramIdTokenValidatorTests
{
    private const string ClientId = "6187611025";

    private static readonly DateTimeOffset Now =
        new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    private RSA _rsa = null!;
    private JsonWebKeySet _jwks = null!;

    [SetUp]
    public void SetUp()
    {
        _rsa = RSA.Create(2048);
        var publicParams = _rsa.ExportParameters(false);
        var jwksJson = "{\"keys\":[{\"kty\":\"RSA\",\"kid\":\"test-key\",\"use\":\"sig\",\"alg\":\"RS256\","
            + $"\"n\":\"{Base64UrlEncoder.Encode(publicParams.Modulus)}\","
            + $"\"e\":\"{Base64UrlEncoder.Encode(publicParams.Exponent)}\"}}]}}";
        _jwks = new JsonWebKeySet(jwksJson);
    }

    [TearDown]
    public void TearDown() => _rsa.Dispose();

    private string CreateToken(
        DateTime? expires,
        string audience = ClientId,
        string? issuer = TelegramIdTokenValidator.Issuer,
        Dictionary<string, object>? claims = null,
        DateTime? notBefore = null)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = expires,
            NotBefore = notBefore ?? Now.UtcDateTime,
            IssuedAt = Now.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(_rsa) { KeyId = "test-key" },
                SecurityAlgorithms.RsaSha256),
            Claims = claims ?? new Dictionary<string, object>
            {
                ["id"] = "123456",
                ["given_name"] = "John",
                ["family_name"] = "Doe",
                ["preferred_username"] = "johndoe",
                ["picture"] = "https://example.com/pic.jpg",
            },
        };
        return handler.CreateEncodedJwt(descriptor);
    }

    [Test]
    public void ValidToken_ShouldPass()
    {
        var token = CreateToken(Now.AddHours(1).UtcDateTime);

        var user = TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now);

        user.Should().BeEquivalentTo(new TelegramOidcUser(
            123456, "John", "Doe", "johndoe", "https://example.com/pic.jpg"));
    }

    [Test]
    public void SubAndNameFallback_ShouldPass()
    {
        var token = CreateToken(
            Now.AddHours(1).UtcDateTime,
            claims: new Dictionary<string, object>
            {
                ["sub"] = "789",
                ["name"] = "John Doe",
            });

        var user = TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now);

        user.Should().BeEquivalentTo(new TelegramOidcUser(789, "John", "Doe", null, null));
    }

    [Test]
    public void WrongAudience_ShouldFail()
    {
        var token = CreateToken(Now.AddHours(1).UtcDateTime, audience: "9999999");

        TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now).Should().BeNull();
    }

    [Test]
    public void WrongIssuer_ShouldFail()
    {
        var token = CreateToken(
            Now.AddHours(1).UtcDateTime, issuer: "https://evil.example.com");

        TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now).Should().BeNull();
    }

    [Test]
    public void Expired_ShouldFail()
    {
        var token = CreateToken(
            Now.AddHours(-1).UtcDateTime, notBefore: Now.AddHours(-2).UtcDateTime);

        TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now).Should().BeNull();
    }

    [Test]
    public void TamperedSignature_ShouldFail()
    {
        var token = CreateToken(Now.AddHours(1).UtcDateTime) + "x";

        TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now).Should().BeNull();
    }

    [Test]
    public void MissingId_ShouldFail()
    {
        var token = CreateToken(
            Now.AddHours(1).UtcDateTime,
            claims: new Dictionary<string, object> { ["given_name"] = "John" });

        TelegramIdTokenValidator.Validate(token, ClientId, _jwks, Now).Should().BeNull();
    }

    [Test]
    public void EmptyInputs_ShouldFail()
    {
        TelegramIdTokenValidator.Validate(null, ClientId, _jwks, Now).Should().BeNull();
        TelegramIdTokenValidator.Validate("x", string.Empty, _jwks, Now).Should().BeNull();
        TelegramIdTokenValidator.Validate("x", ClientId, new JsonWebKeySet("{\"keys\":[]}"), Now)
            .Should().BeNull();
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
