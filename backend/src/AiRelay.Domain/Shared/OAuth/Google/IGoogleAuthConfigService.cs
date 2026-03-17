using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Domain.Shared.OAuth.Google;

/// <summary>
/// Google OAuth 配置服务接口
/// </summary>
public interface IGoogleAuthConfigService
{
    /// <summary>
    /// 获取指定平台的 Google OAuth 配置
    /// </summary>
    GoogleAuthConfig GetConfig(ProviderPlatform platform);
}
