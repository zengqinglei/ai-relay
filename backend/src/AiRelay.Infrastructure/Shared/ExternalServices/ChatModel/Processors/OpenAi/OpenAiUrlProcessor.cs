using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.OpenAi;

public class OpenAiUrlProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl)
            ? options.BaseUrl
            : options.Platform == ProviderPlatform.OPENAI_OAUTH ? "https://chatgpt.com" : "https://api.openai.com";

        // 统一转换为 Responses API 路径
        var relativePath = options.Platform == ProviderPlatform.OPENAI_OAUTH
            ? "/backend-api/codex/responses"
            : "/v1/responses";

        // 保留 /responses/ 后的子路径（如 /responses/compact）
        var downPath = down.RelativePath?.Trim() ?? "";
        if (downPath.Contains("/responses/"))
        {
            var idx = downPath.IndexOf("/responses/", StringComparison.Ordinal);
            relativePath += downPath[(idx + "/responses".Length)..];
        }

        up.RelativePath = relativePath;
        up.QueryString = down.QueryString;

        return Task.CompletedTask;
    }
}
