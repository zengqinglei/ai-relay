using Leistd.Exception.Core;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Dto;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.OpenAiCompatible;

/// <summary>
/// OpenAI Compatible URL 处理器
/// 支持自定义 BaseUrl 并透传路径
/// </summary>
public class OpenAiCompatibleUrlRequestProcessor(ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var baseUrl = options.BaseUrl?.TrimEnd('/') ?? throw new BadRequestException("OpenAICompatible 必须配置 BaseUrl");
        var relPath = down.RelativePath?.TrimStart('/') ?? "";

        // 智能去重：处理 BaseUrl 包含 /v1 且 relPath 也包含 v1 的情况
        if (baseUrl.EndsWith("/v1") && relPath.StartsWith("v1/"))
        {
            relPath = relPath["v1/".Length..];
        }

        up.BaseUrl = baseUrl;
        up.RelativePath = "/" + relPath;
        up.QueryString = down.QueryString;

        return Task.CompletedTask;
    }
}
