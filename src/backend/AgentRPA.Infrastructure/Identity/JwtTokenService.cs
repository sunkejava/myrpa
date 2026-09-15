using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgentRPA.Application.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace AgentRPA.Infrastructure.Identity;

public sealed class JwtTokenService(IConfiguration configuration) : IJwtTokenService
{
    public string CreateToken(LoginResult user)
    {
        var section = configuration.GetSection("AgentRPA:Jwt");
        var key = section["SigningKey"] ?? throw new InvalidOperationException("未配置 JWT SigningKey。");
        var issuer = section["Issuer"] ?? "AgentRPA";
        var audience = section["Audience"] ?? "AgentRPA.Api";
        var lifetimeMinutes = int.TryParse(section["LifetimeMinutes"], out var value) ? Math.Clamp(value, 5, 1440) : 60;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("display_name", user.DisplayName)
        };
        claims.AddRange(user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(issuer, audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(lifetimeMinutes), credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
