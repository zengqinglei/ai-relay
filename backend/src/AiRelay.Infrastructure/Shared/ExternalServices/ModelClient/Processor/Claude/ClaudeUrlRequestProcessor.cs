using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Claude;

public class ClaudeUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        up.BaseUrl = !string.IsNullOrEmpty(options.BaseUrl)
            ? options.BaseUrl
            : "https://api.anthropic.com";

        var relativePath = down.RelativePath;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/'))
            relativePath = "/" + relativePath;
        up.RelativePath = relativePath;
        up.QueryString = down.QueryString;

        // 聊天接口特有处理
        if (up.RelativePath.Contains("/v1/messages", StringComparison.OrdinalIgnoreCase))
        {
            // 构建 QueryString（追加 beta=true）
            if (string.IsNullOrEmpty(up.QueryString))
            {
                up.QueryString = "?beta=true";
            }
            else if (!up.QueryString.Contains("beta=", StringComparison.OrdinalIgnoreCase))
            {
                var separator = up.QueryString.Contains('?') ? "&" : "?";
                up.QueryString = $"{up.QueryString}{separator}beta=true";
            }
        }
        return Task.CompletedTask;
    }
}
