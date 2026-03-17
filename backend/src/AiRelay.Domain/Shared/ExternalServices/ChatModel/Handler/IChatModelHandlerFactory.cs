using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 聊天模型客户端工厂接口
/// </summary>
public interface IChatModelHandlerFactory
{
    /// <summary>
    /// 根据账户实体创建客户端
    /// </summary>
    /// <param name="platform">平台类型</param>
    /// <param name="accessToken">访问令牌</param>
    /// <param name="baseUrl">基础 URL</param>
    /// <param name="extraProperties">额外属性</param>
    /// <param name="shouldMimicOfficialClient">是否伪装为官方客户端（默认 true）</param>
    /// <returns>已配置的客户端</returns>
    IChatModelHandler CreateHandler(ProviderPlatform platform, string accessToken, string? baseUrl = null, Dictionary<string, string>? extraProperties = null, bool shouldMimicOfficialClient = true);

    /// <summary>
    /// 根据平台类型获取客户端 (未配置)
    /// </summary>
    /// <param name="platform">平台类型</param>
    /// <returns>客户端实例</returns>
    IChatModelHandler CreateHandler(ProviderPlatform platform);
}
