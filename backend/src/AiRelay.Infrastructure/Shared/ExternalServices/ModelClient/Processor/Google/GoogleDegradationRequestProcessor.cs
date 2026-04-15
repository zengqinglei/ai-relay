using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Context;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.Processor;
using AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Processor.Google;

/// <summary>
/// Google 系平台统一降级处理器（Antigravity / Gemini OAuth / Gemini ApiKey）
///
/// Level 0  : 注入已缓存的 thoughtSignature
/// Level 1  : 移除 thoughtSignature
/// Level 2+ : 移除所有 FunctionDeclaration
///
/// 自动适配两种 Body 结构：
///   - v1internal 包装（Antigravity / Gemini OAuth）→ 从 body["request"] 取内层 payload
///   - 裸请求（Gemini ApiKey）                       → 直接操作 body 本身
/// </summary>
public class GoogleDegradationRequestProcessor(
    int degradationLevel,
    GoogleSignatureCleaner googleSignatureCleaner) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (up.BodyJson == null) return Task.CompletedTask;

        // 自动兼容 v1internal 包装结构
        var payload = up.BodyJson.ContainsKey("request")
            ? up.BodyJson["request"] as JsonObject
            : up.BodyJson;

        if (payload == null) return Task.CompletedTask;

        if (degradationLevel == 0)
        {
            // 若报文中已含签名，则无需注入
            if (down.ExtractedProps.ContainsKey("google.has_signature")) return Task.CompletedTask;

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
