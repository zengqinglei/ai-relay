using AiRelay.Domain.Shared.ExternalServices.ModelClient.Constants;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Antigravity;

public class AntigravityHeaderRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        // 白名单透传 + 强制覆盖
        foreach (var (key, (allowPassthrough, defaultValue, forceOverride)) in AntigravityMimicDefaults.Headers)
        {
            if (allowPassthrough &&
                down.Headers.TryGetValue(key, out var downValue) &&
                !string.IsNullOrWhiteSpace(downValue))
            {
                up.Headers[key] = downValue;
            }
            else if (defaultValue != null && (forceOverride || !up.Headers.ContainsKey(key)))
            {
                up.Headers[key] = defaultValue;
            }
        }

        up.Headers["Authorization"] = $"Bearer {options.Credential}";

        return Task.CompletedTask;
    }
}
