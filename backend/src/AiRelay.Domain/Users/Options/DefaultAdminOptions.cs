namespace AiRelay.Domain.Users.Options;

/// <summary>
/// 默认管理员配置选项
/// </summary>
public class DefaultAdminOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "DefaultAdmin";

    /// <summary>
    /// 用户名
    /// </summary>
    public string Username { get; set; } = "admin";

    /// <summary>
    /// 密码
    /// </summary>
    public string Password { get; set; } = "Admin@123456";

    /// <summary>
    /// 邮箱
    /// </summary>
    public string Email { get; set; } = "admin@ai-relay.com";

    /// <summary>
    /// 昵称
    /// </summary>
    public string? Nickname { get; set; }
}
