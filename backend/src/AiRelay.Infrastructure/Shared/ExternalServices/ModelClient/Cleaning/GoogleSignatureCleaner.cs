using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiRelay.Domain.Shared.ExternalServices.ModelClient.SignatureCache;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ModelClient.Cleaning;

/// <summary>
/// Google API 签名清洗器（通用于 Gemini Account 和 Antigravity）
/// 负责处理 Gemini 请求中的 thoughtSignature 注入、移除和降级策略
/// </summary>
public class GoogleSignatureCleaner(
    ISignatureCache signatureCache,
    ILogger<GoogleSignatureCleaner> logger)
{
    /// <summary>
    /// 注入缓存的签名到最后一个 assistant 消息
    /// </summary>
    public void InjectCachedSignature(JsonObject requestJson, string sessionId)
    {
        try
        {
            var cachedSignature = signatureCache.GetSignature(sessionId);
            if (string.IsNullOrEmpty(cachedSignature)) return;

            if (requestJson["contents"] is JsonArray contents)
            {
                for (int i = contents.Count - 1; i >= 0; i--)
                {
                    if (contents[i] is JsonObject content && content["role"]?.GetValue<string>() == "assistant")
                    {
                        if (content["parts"] is JsonArray parts)
                        {
                            if (!parts.Any(p => p?["thoughtSignature"] != null))
                            {
                                parts.Insert(0, new JsonObject { ["thoughtSignature"] = cachedSignature });
                                logger.LogDebug("注入签名 Session: {Session}", sessionId);
                            }
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "InjectCachedSignature 失败 Session: {Session}", sessionId);
        }
    }

    /// <summary>
    /// 深度清理：根据降级级别递归清理请求内容
    /// </summary>
    /// <param name="requestJson">请求 JSON</param>
    /// <param name="degradationLevel">
    /// 0: 不清理
    /// 1: 移除所有 signature
    /// 2+: 移除 signature + 移除所有历史中的 functionCall/toolCall/functionResponse/toolResponse
    /// </param>
    public void DeepCleanForDegradation(JsonObject requestJson, int degradationLevel)
    {
        try
        {
            InternalDeepClean(requestJson, degradationLevel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GoogleSignatureCleaner: DeepCleanForDegradation 失败 (Level {Level})", degradationLevel);
        }
    }

    private void InternalDeepClean(JsonObject requestJson, int degradationLevel)
    {
        if (degradationLevel <= 0) return;
        if (requestJson["contents"] is not JsonArray contents) return;

        int removedPartCount = 0;
        int cleanedPartCount = 0;

        foreach (var content in contents)
        {
            if (content is not JsonObject contentObj || contentObj["parts"] is not JsonArray parts)
                continue;

            for (int i = parts.Count - 1; i >= 0; i--)
            {
                if (parts[i] is not JsonObject partObj) continue;

                // 级别 1 & 2：必清签名和思维标记
                if (partObj.ContainsKey("thoughtSignature") || partObj.ContainsKey("thought"))
                {
                    // 核心细节：保留 thought 内容并降级为普通文本，防止丢失上下文
                    if (partObj.TryGetPropertyValue("thought", out var thoughtVal) && thoughtVal != null)
                    {
                        var thoughtStr = thoughtVal.GetValue<string>();
                        if (!string.IsNullOrWhiteSpace(thoughtStr))
                        {
                            logger.LogDebug("正在合并 Google Thought 文本至 Part");
                            if (partObj.TryGetPropertyValue("text", out var textVal) && textVal != null)
                            {
                                var existingText = textVal.GetValue<string>();
                                partObj["text"] = $"{existingText}\n\n[Original Thought]:\n{thoughtStr}";
                            }
                            else
                            {
                                partObj["text"] = $"[Original Thought]:\n{thoughtStr}";
                            }
                        }
                    }

                    if (partObj.ContainsKey("thoughtSignature")) logger.LogDebug("已移除 thoughtSignature");
                    partObj.Remove("thoughtSignature");
                    partObj.Remove("thought");
                    cleanedPartCount++;
                }

                // 级别 2：清理所有工具调用历史
                if (degradationLevel >= 2)
                {
                    if (partObj.ContainsKey("functionCall") ||
                        partObj.ContainsKey("toolCall") ||
                        partObj.ContainsKey("functionResponse") ||
                        partObj.ContainsKey("toolResponse"))
                    {
                        logger.LogDebug("已移除工具节点: {PartKeys}", string.Join(", ", partObj.Select(p => p.Key)));
                        parts.RemoveAt(i);
                        removedPartCount++;
                    }
                }
                else if (degradationLevel == 1)
                {
                    // 核心改进：Level 1 保留工具调用节点时，必须为其补上 bypass 签名
                    // 否则 Code Assist 等严格上游会报 "missing a thought_signature" 导致请求直接失败
                    if (partObj.ContainsKey("functionCall"))
                    {
                        partObj["thoughtSignature"] = "skip_thought_signature_validator";
                    }
                }
            }
        }

        // 级别 2：清理顶层定义
        if (degradationLevel >= 2)
        {
            if (requestJson.ContainsKey("tools")) requestJson.Remove("tools");
            if (requestJson.ContainsKey("tool_config")) requestJson.Remove("tool_config");
        }

        // 如果做了任何清理，禁用思维配置以保持一致
        if (requestJson.TryGetPropertyValue("thinking_config", out var config) && config is JsonObject configObj)
        {
            configObj["include_thoughts"] = false;
        }

        if (cleanedPartCount > 0 || removedPartCount > 0)
        {
            logger.LogInformation("执行深度清理 (Level {Level}): 清理了 {Cleaned} 个签名并降级思维块，移除了 {Removed} 个工具节点",
                degradationLevel, cleanedPartCount, removedPartCount);
        }

        // 清理由于移除节点导致的空 parts 或空 contents，避免被上游因结构空洞而拒绝
        if (removedPartCount > 0)
        {
            GeminiContentPartsCleaner.FilterEmptyParts(requestJson);
        }
    }

    /// <summary>
    /// 检测响应 body 是否为签名错误
    /// </summary>
    public static bool IsSignatureError(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;

        var lowerBody = body.ToLowerInvariant();
        return lowerBody.Contains("thought signature is not valid") ||
               lowerBody.Contains("missing a thought_signature") ||
               lowerBody.Contains("corrupted thought signature") ||
               lowerBody.Contains("invalid thought signature") ||
               (lowerBody.Contains("thinking block") && lowerBody.Contains("not valid")) ||
               (lowerBody.Contains("signature") && lowerBody.Contains("not match"));
    }

    /// <summary>
    /// 创建签名提取流，用于从 SSE 流中提取 thoughtSignature 并缓存
    /// </summary>
    public Stream CreateSignatureExtractingStream(Stream innerStream, string sessionId)
    {
        return new SignatureExtractingStream(innerStream, sessionId, signatureCache, logger);
    }

    /// <summary>
    /// 签名提取流 - 拦截响应流，提取 thoughtSignature 并缓存
    /// </summary>
    private sealed class SignatureExtractingStream(
        Stream innerStream,
        string sessionId,
        ISignatureCache signatureCache,
        ILogger logger) : Stream
    {
        private readonly MemoryStream _buffer = new();
        private bool _signatureExtracted;
        private const int MaxBufferSize = 512 * 1024;

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_signatureExtracted)
            {
                if (_buffer.Length + count <= MaxBufferSize)
                {
                    await _buffer.WriteAsync(buffer, offset, count, cancellationToken);
                    TryExtractSignature();
                }
                else
                {
                    _signatureExtracted = true;
                    _buffer.SetLength(0);
                }
            }
            await innerStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_signatureExtracted)
            {
                if (_buffer.Length + buffer.Length <= MaxBufferSize)
                {
                    await _buffer.WriteAsync(buffer, cancellationToken);
                    TryExtractSignature();
                }
                else
                {
                    _signatureExtracted = true;
                    _buffer.SetLength(0);
                }
            }
            await innerStream.WriteAsync(buffer, cancellationToken);
        }

        private void TryExtractSignature()
        {
            if (_signatureExtracted) return;
            var originalPos = _buffer.Position;
            _buffer.Position = 0;
            using var reader = new StreamReader(_buffer, Encoding.UTF8, leaveOpen: true);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (!line.StartsWith("data:")) continue;
                var json = line[5..].TrimStart();
                if (json == "[DONE]") break;

                try
                {
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("response", out var w)) root = w;

                    if (ExtractSignature(root, out var sig))
                    {
                        signatureCache.CacheSignature(sessionId, sig!);
                        logger.LogDebug("提取并缓存签名 Session: {Session}, Len: {Len}", sessionId, sig!.Length);
                        _buffer.SetLength(0);
                        _signatureExtracted = true;
                        break;
                    }
                }
                catch { }
            }
            if (!_signatureExtracted) _buffer.Position = originalPos;
        }

        private static bool ExtractSignature(JsonElement root, out string? signature)
        {
            signature = null;
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
                            signature = sig.GetString();
                            return !string.IsNullOrEmpty(signature);
                        }
                    }
                }
            }
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _buffer.Dispose();
            base.Dispose(disposing);
        }

        public override bool CanRead => innerStream.CanRead;
        public override bool CanSeek => innerStream.CanSeek;
        public override bool CanWrite => innerStream.CanWrite;
        public override long Length => innerStream.Length;
        public override long Position { get => innerStream.Position; set => innerStream.Position = value; }
        public override void Flush() => innerStream.Flush();
        public override int Read(byte[] buffer, int offset, int count) => innerStream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) => innerStream.Seek(offset, origin);
        public override void SetLength(long value) => innerStream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => innerStream.Write(buffer, offset, count);
    }
}
