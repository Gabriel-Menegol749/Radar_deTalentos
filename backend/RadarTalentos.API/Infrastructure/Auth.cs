using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using RadarTalentos.API.Entities;

namespace RadarTalentos.API.Infrastructure;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "RadarTalentos";
    public string Audience { get; set; } = "RadarTalentos.Web";
    public int ExpiresHours { get; set; } = 8;
}

public class TokenService(JwtOptions options)
{
    public (string Token, DateTime ExpiresAt) Create(User user)
    {
        var expires = DateTime.UtcNow.AddHours(options.ExpiresHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("name", user.Name),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("role", user.Role),
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(options.Issuer, options.Audience, claims, expires: expires, signingCredentials: credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : Guid.Empty;

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin);
}
