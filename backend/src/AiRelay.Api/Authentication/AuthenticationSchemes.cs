namespace AiRelay.Api.Authentication;

/// <summary>
/// 认证方案常量
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>
    /// API Key 认证方案（用于 AI 代理服务）
    /// </summary>
    public const string ApiKey = "ApiKey";

    /// <summary>
    /// JWT Bearer 认证方案（预留，用于管理后台）
    /// </summary>
    public const string Bearer = "Bearer";
}
