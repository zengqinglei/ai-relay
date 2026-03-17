namespace AiRelay.Api.Authentication;

/// <summary>
/// 授权策略常量
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// AI 代理策略：用于 /gemini、/claude、/openai 路由
    /// 需要 API Key 认证
    /// </summary>
    public const string AiProxyPolicy = "AiProxyPolicy";

    /// <summary>
    /// 管理员策略（预留）：用于管理后台接口
    /// 需要 JWT Bearer 认证 + Admin 角色
    /// </summary>
    public const string AdminPolicy = "AdminPolicy";

    /// <summary>
    /// 用户策略（预留）：用于普通用户接口
    /// 需要 JWT Bearer 认证
    /// </summary>
    public const string UserPolicy = "UserPolicy";
}
