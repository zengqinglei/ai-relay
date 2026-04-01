using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;

/// <summary>
/// 聊天模型连接配置
/// </summary>
/// <param name="Platform">提供商平台类型</param>
/// <param name="Credential">认证凭据 (Access Token 或 API Key)</param>
/// <param name="BaseUrl">基础 API 地址 (可选)</param>
public record ChatModelConnectionOptions(
    ProviderPlatform Platform,
    string Credential,
    string? BaseUrl = null)
{
    /// <summary>
    /// 是否伪装为官方客户端
    /// </summary>
    public bool ShouldMimicOfficialClient { get; init; } = true;

    /// <summary>
    /// 额外属性（存储平台特定元数据，如 chatgpt_account_id, project_id）
    /// </summary>
    public Dictionary<string, string> ExtraProperties { get; init; } = new();

    /// <summary>
    /// 模型白名单（限制可接受的模型）
    /// </summary>
    public List<string>? ModelWhites { get; init; }

    /// <summary>
    /// 模型映射规则（转换模型名称，支持通配符）
    /// </summary>
    public Dictionary<string, string>? ModelMapping { get; init; }
}
