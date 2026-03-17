namespace AiRelay.Domain.Shared.Security.PasswordHash;

/// <summary>
/// 密码哈希服务接口
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 哈希密码
    /// </summary>
    /// <param name="password">明文密码</param>
    /// <returns>密码哈希</returns>
    string HashPassword(string password);

    /// <summary>
    /// 验证密码
    /// </summary>
    /// <param name="hashedPassword">密码哈希</param>
    /// <param name="providedPassword">提供的明文密码</param>
    /// <returns>是否匹配</returns>
    bool VerifyPassword(string hashedPassword, string providedPassword);
}
