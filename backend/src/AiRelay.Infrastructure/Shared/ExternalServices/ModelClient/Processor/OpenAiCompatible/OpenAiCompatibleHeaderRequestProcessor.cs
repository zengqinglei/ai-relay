using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAiCompatible;

/// <summary>
/// OpenAI Compatible Header 处理器
/// 支持 API Key 注入与 SDK 头透传
/// </summary>
public class OpenAiCompatibleHeaderRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 1. 根据配置决定透传哪些 Header
        foreach (var kvp in down.Headers)
        {
            if (OpenAiCompatibleMimicDefaults.Headers.TryGetValue(kvp.Key, out var config) &&
                config.AllowPassthrough &&
                !string.IsNullOrEmpty(kvp.Value))
            {
                up.Headers[kvp.Key] = kvp.Value;
            }
        }

        // 2. 强制覆盖认证信息
        up.Headers.Remove("x-api-key");
        up.Headers.Remove("x-goog-api-key");
        up.Headers.Remove("cookie");
        up.Headers["Authorization"] = $"Bearer {options.Credential}";

        // 3. 伪装官方客户端逻辑（如果开启）
        if (options.ShouldMimicOfficialClient)
        {
            foreach (var (key, (_, defaultValue, forceOverride)) in OpenAiCompatibleMimicDefaults.Headers)
            {
                if (defaultValue == null) continue;

                if (forceOverride || !up.Headers.ContainsKey(key))
                {
                    up.Headers[key] = defaultValue;
                }
            }
        }

        return Task.CompletedTask;
    }
}
