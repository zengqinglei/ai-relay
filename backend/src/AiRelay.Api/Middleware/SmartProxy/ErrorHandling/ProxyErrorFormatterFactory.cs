using AiRelay.Domain.ProviderAccounts.ValueObjects;

namespace AiRelay.Api.Middleware.SmartProxy.ErrorHandling;

/// <summary>
/// 代理错误格式化器工厂
/// </summary>
public class ProxyErrorFormatterFactory(IEnumerable<IProxyErrorFormatter> formatters)
{
    /// <summary>
    /// 根据平台获取对应的错误格式化器
    /// 如果找不到精确匹配，默认返回 OpenAI 的通用格式，因为大多数支持兼容的客户端都能看懂
    /// </summary>
    public IProxyErrorFormatter GetFormatter(ProviderPlatform platform) =>
        formatters.FirstOrDefault(f => f.Platform == platform) ??
        formatters.FirstOrDefault(f => f.Platform == ProviderPlatform.OPENAI_OAUTH)!;
}
