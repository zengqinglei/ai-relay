namespace Leistd.Security.Claims;

/// <summary>
/// 自定义 Claims 类型定义
/// </summary>
/// <remarks>
/// 仅包含与 System.Security.Claims.ClaimTypes 不同的自定义字段。
/// 标准字段请直接使用 System.Security.Claims.ClaimTypes。
/// </remarks>
public static class CustomClaimTypes
{
    /// <summary>
    /// 客户端标识符（用于 API Key 认证）
    /// </summary>
    public const string ClientId = "client_id";

    /// <summary>
    /// 会话标识符（OIDC 标准）
    /// </summary>
    public const string SessionId = "sid";

    /// <summary>
    /// 身份提供者（OIDC 标准）
    /// </summary>
    /// <remarks>
    /// 值示例: github, google, microsoft
    /// </remarks>
    public const string IdentityProvider = "idp";
}
