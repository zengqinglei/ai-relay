using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Claude;

public class ClaudeUrlProcessor(bool isChatApi, ChatModelConnectionOptions options) : IRequestProcessor
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

        if (!isChatApi)
        {
            return Task.CompletedTask;
        }
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

        return Task.CompletedTask;
    }
}
