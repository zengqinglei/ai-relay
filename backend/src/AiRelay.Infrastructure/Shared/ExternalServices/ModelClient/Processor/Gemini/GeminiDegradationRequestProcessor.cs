using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Gemini;

/// <summary>
/// Gemini OAuth 降级处理器 + 签名注入（level 0 时注入，level > 0 时降级）
/// Level 1: 移除 thoughtSignature
/// Level 2+: 移除所有 FunctionDeclaration
/// </summary>
public class GeminiDegradationRequestProcessor(
    int degradationLevel,
    GoogleSignatureCleaner googleSignatureCleaner) : IRequestProcessor
{

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (up.BodyJson == null) return Task.CompletedTask;

        // 获取内层 payload（已封装在 v1internal 结构中）
        var payload = up.BodyJson.ContainsKey("request")
            ? up.BodyJson["request"] as JsonObject
            : up.BodyJson;

        if (payload == null) return Task.CompletedTask;

        if (degradationLevel == 0)
        {
            if (!string.IsNullOrEmpty(down.SessionId))
                googleSignatureCleaner.InjectCachedSignature(payload, down.SessionId);
        }
        else
        {
            googleSignatureCleaner.DeepCleanForDegradation(payload, degradationLevel);
        }

        return Task.CompletedTask;
    }
}
