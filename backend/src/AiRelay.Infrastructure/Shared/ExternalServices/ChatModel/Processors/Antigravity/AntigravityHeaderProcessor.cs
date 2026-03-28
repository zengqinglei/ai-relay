using AiRelay.Domain.Shared.ExternalServices.ChatModel.Constants;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;

public class AntigravityHeaderProcessor(ChatModelConnectionOptions options) : IRequestProcessor
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
