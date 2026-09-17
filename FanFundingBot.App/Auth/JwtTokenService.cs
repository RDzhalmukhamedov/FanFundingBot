using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FanFundingBot.App.Auth;

public sealed class JwtTokenService
{
    public const string SubjectClaim = "sub";
    public const string CookieName = "ffbot_auth";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string CreateToken(long userId, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now = DateTime.UtcNow;

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(SubjectClaim, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            ],
            notBefore: now,
            expires: now.AddDays(_options.ExpiresDays),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
