using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

public class GeminiApiKeyUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl)
            ? options.BaseUrl
            : "https://generativelanguage.googleapis.com";

        var relativePath = down.RelativePath ?? string.Empty;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;
        up.RelativePath = relativePath;
        up.QueryString = down.QueryString;

        if (!up.RelativePath.EndsWith(":streamGenerateContent"))
        {
            return Task.CompletedTask;
        }
        // 构建 QueryString（追加 alt=sse）
        if (string.IsNullOrEmpty(up.QueryString))
        {
            up.QueryString = "?alt=sse";
        }
        else if (!up.QueryString.Contains("alt=", StringComparison.OrdinalIgnoreCase))
        {
            var separator = up.QueryString.Contains('?') ? "&" : "?";
            up.QueryString = $"{up.QueryString}{separator}alt=sse";
        }

        return Task.CompletedTask;
    }
}
