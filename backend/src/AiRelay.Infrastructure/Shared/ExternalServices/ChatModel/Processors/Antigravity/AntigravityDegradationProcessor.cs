using System.Text.Json.Nodes;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Processors;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Antigravity;

/// <summary>
/// Antigravity 降级处理器 + 签名注入（level 0 时注入，level > 0 时降级）
/// Level 1: 移除 thoughtSignature
/// Level 2+: 移除所有 FunctionDeclaration
/// </summary>
public class AntigravityDegradationProcessor(
    int degradationLevel,
    GoogleSignatureCleaner googleSignatureCleaner,
    ILogger logger) : IRequestProcessor
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
            // 正常级别：注入签名（如果 SessionHash 存在）
            if (!string.IsNullOrEmpty(down.SessionHash))
                googleSignatureCleaner.InjectCachedSignature(payload, down.SessionHash);
        }
        else if (degradationLevel == 1)
        {
            googleSignatureCleaner.RemoveThoughtSignatures(payload);
            logger.LogWarning("应用降级级别 1: 移除 thoughtSignature");
        }
        else if (degradationLevel >= 2)
        {
            googleSignatureCleaner.RemoveFunctionDeclarations(payload);
            logger.LogWarning("应用降级级别 2: 移除所有 FunctionDeclaration");
        }

        return Task.CompletedTask;
    }
}
