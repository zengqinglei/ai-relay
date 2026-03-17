using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 连接验证器接口：账号创建/检测场景使用
/// </summary>
public interface IConnectionValidator
{
    /// <summary>
    /// 验证连接（握手 / 获取项目 ID 等平台特定操作）
    /// </summary>
    Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户配额信息，不支持的平台返回 null
    /// </summary>
    Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken cancellationToken = default);
}
