using System.Text.Json;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Processors.Response.Google;

/// <summary>
/// Google 系专属：提取 thoughtSignature 并缓存
/// 替代原 GoogleInternalChatModelHandlerBase.GetSseLineCallback → TryExtractAndCacheSignature
/// </summary>
public class CacheSignatureResponseProcessor(
    ISignatureCache signatureCache,
    string? sessionId,
    ILogger logger) : IResponseProcessor
{
    public bool RequiresMutation => false;

    public Task ProcessAsync(StreamEvent evt, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(sessionId)) return Task.CompletedTask;
        if (evt.Type == StreamEventType.Error) return Task.CompletedTask;
        if (string.IsNullOrEmpty(evt.SseLine)) return Task.CompletedTask;

        if (!evt.SseLine.StartsWith("data:")) return Task.CompletedTask;

        var json = evt.SseLine[5..].TrimStart();
        if (json == "[DONE]") return Task.CompletedTask;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("response", out var responseObj))
                root = responseObj;

            if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
            {
                var candidate = candidates[0];
                if (candidate.TryGetProperty("content", out var content) &&
                    content.TryGetProperty("parts", out var parts))
                {
                    foreach (var part in parts.EnumerateArray())
                    {
                        if (part.TryGetProperty("thoughtSignature", out var sig))
                        {
                            var signature = sig.GetString();
                            if (!string.IsNullOrEmpty(signature))
                            {
                                signatureCache.CacheSignature(sessionId, signature);
                                logger.LogDebug("提取并缓存签名 Session: {Session}", sessionId);
                                return Task.CompletedTask;
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return Task.CompletedTask;
    }
}
