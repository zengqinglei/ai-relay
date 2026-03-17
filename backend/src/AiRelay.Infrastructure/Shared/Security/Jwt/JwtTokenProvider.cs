using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using AiRelay.Domain.Shared.Security.Jwt;
using AiRelay.Domain.Shared.Security.Jwt.Options;
using Leistd.Exception.Core;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AiRelay.Infrastructure.Shared.Security.Jwt;

/// <summary>
/// JWT Token 服务实现
/// </summary>
public class JwtTokenProvider(IOptions<JwtOptions> jwtOptions) : IJwtTokenProvider
{
    private readonly JwtOptions _jwtOptions = jwtOptions.Value;

    public string GenerateToken(Guid userId, string username, string email, string[] roles)
    {
        if (string.IsNullOrEmpty(_jwtOptions.SecretKey))
            throw new NotFoundException("JWT SecretKey 未配置");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        List<Claim> claims =
        [
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, username),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        ];

        // 添加角色 Claims
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtOptions.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
