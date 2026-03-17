namespace AiRelay.Domain.Shared.Security.Jwt;

/// <summary>
/// JWT Token 服务接口
/// </summary>
public interface IJwtTokenProvider
{
    /// <summary>
    /// 生成 JWT Token
    /// </summary>
    /// <param name="userId">用户 ID</param>
    /// <param name="username">用户名</param>
    /// <param name="email">邮箱</param>
    /// <param name="roles">角色列表</param>
    /// <returns>JWT Token</returns>
    string GenerateToken(Guid userId, string username, string email, string[] roles);

    /// <summary>
    /// 生成刷新令牌
    /// </summary>
    /// <returns>刷新令牌</returns>
    string GenerateRefreshToken();
}
