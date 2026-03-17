namespace Leistd.Security.Clients;

/// <summary>
/// 当前客户端信息（API Key 认证场景）
/// </summary>
public interface ICurrentClient
{
    /// <summary>
    /// 是否已认证（是否为 API Key 认证）
    /// </summary>
    bool IsAuthenticated { get; }

    /// <summary>
    /// 客户端标识符
    /// </summary>
    string? ClientId { get; }

    /// <summary>
    /// API Key 标识符
    /// </summary>
    Guid? ApiKeyId { get; }

    /// <summary>
    /// API Key 创建者 ID（如果有）
    /// </summary>
    Guid? CreatorId { get; }
}
