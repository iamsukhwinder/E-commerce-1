using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ecommerce.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Ecommerce.Api.Services;

public sealed class TokenService(IConfiguration configuration)
{
    public object Create(User user)
    {
        var settings = configuration.GetSection("Jwt");
        var expires = DateTime.UtcNow.AddMinutes(settings.GetValue<int>("ExpiryMinutes", 120));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.Role)
        };
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings["Key"]!)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(settings["Issuer"], settings["Audience"], claims, expires: expires, signingCredentials: credentials);
        return new { token = new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt = expires, user = new { user.Id, user.Name, user.Email, user.Role, user.Phone, user.AvatarUrl } };
    }
}
