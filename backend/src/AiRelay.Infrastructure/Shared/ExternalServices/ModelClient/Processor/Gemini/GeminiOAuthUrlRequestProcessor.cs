using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

public class GeminiOAuthUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl)
            ? options.BaseUrl
            : "https://cloudcode-pa.googleapis.com";

        var relativePath = down.RelativePath ?? string.Empty;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;
        up.RelativePath = relativePath;
        up.QueryString = down.QueryString;

        // 强制转为 /v1internal:xxx
        if (!relativePath.StartsWith("/v1internal", StringComparison.OrdinalIgnoreCase))
        {
            if (relativePath.Contains(':'))
            {
                var parts = relativePath.Split(':');
                if (parts.Length > 1)
                {
                    var potentialOp = parts[1].Split('?')[0];
                    if (!string.IsNullOrEmpty(potentialOp))
                    {
                        up.RelativePath = $"/v1internal:{potentialOp}";
                    }
                }
            }
        }
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
