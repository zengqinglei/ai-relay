using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;

public class AntigravityHeaderProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.Headers["User-Agent"] = "antigravity/1.20.6 windows/amd64";
        up.Headers["Authorization"] = $"Bearer {options.Credential}";
        up.Headers["Content-Type"] = "application/json";

        // 透传 anthropic-* headers
        if (down.Headers.TryGetValue("anthropic-version", out var version) &&
            !string.IsNullOrWhiteSpace(version))
            up.Headers["anthropic-version"] = version;

        if (down.Headers.TryGetValue("anthropic-beta", out var beta) &&
            !string.IsNullOrWhiteSpace(beta))
            up.Headers["anthropic-beta"] = beta;

        return Task.CompletedTask;
    }
}
