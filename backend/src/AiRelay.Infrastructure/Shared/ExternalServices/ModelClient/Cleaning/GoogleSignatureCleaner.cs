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

    /// <summary>
    /// 第一次降级：移除所有 thoughtSignature（保留 FunctionDeclaration）
    /// </summary>
    public void RemoveThoughtSignatures(JsonObject requestJson)
    {
        if (requestJson["contents"] is not JsonArray contents) return;

        int removedCount = 0;
        foreach (var content in contents)
        {
            if (content is not JsonObject contentObj || contentObj["parts"] is not JsonArray parts)
                continue;

            foreach (var part in parts)
            {
                if (part is JsonObject partObj && partObj.ContainsKey("thoughtSignature"))
                {
                    partObj.Remove("thoughtSignature");
                    removedCount++;
                }
            }
        }

        if (removedCount > 0)
        {
            logger.LogInformation("降级级别 1: 移除了 {Count} 个 thoughtSignature", removedCount);
        }
    }

    /// <summary>
    /// 第二次降级：移除所有 FunctionDeclaration（tools 字段）
    /// </summary>
    public void RemoveFunctionDeclarations(JsonObject requestJson)
    {
        // 先执行第一次降级（移除签名）
        RemoveThoughtSignatures(requestJson);

        // 移除所有 tools
        if (requestJson.ContainsKey("tools"))
        {
            requestJson.Remove("tools");
            logger.LogInformation("降级级别 2: 移除了所有 FunctionDeclaration (tools)");
        }

        // 移除 tool_config（如果存在）
        if (requestJson.ContainsKey("tool_config"))
        {
            requestJson.Remove("tool_config");
            logger.LogDebug("降级级别 2: 移除了 tool_config");
        }
    }

    /// <summary>
    /// 降级无签名的 thinking blocks（将 thought=true 的块降级为普通文本）
    /// </summary>
    public void DowngradeThinkingBlocksWithoutSignature(JsonObject requestJson)
    {
        if (requestJson["contents"] is not JsonArray contents) return;

        // 遍历所有消息内容
        foreach (var content in contents)
        {
            if (content is not JsonObject contentObj || contentObj["parts"] is not JsonArray parts) continue;

            foreach (var part in parts)
            {
                if (part is not JsonObject partObj) continue;

                // 检查是否为 thinking block
                if (partObj.TryGetPropertyValue("thought", out var thoughtVal) && thoughtVal?.GetValue<bool>() == true)
                {
                    // 如果缺少 thoughtSignature，降级为普通文本
                    if (!partObj.ContainsKey("thoughtSignature"))
                    {
                        // 移除 thought 标记，保留 text，即降级为普通文本块
                        partObj.Remove("thought");
                        logger.LogDebug("由于缺少签名，thinking 块降级为普通文本");
                    }
                }
            }
        }

        // 同时移除 thinkingConfig 以禁用 thinking 模式，防止上游因格式不匹配报错
        if (requestJson.ContainsKey("generationConfig"))
        {
            var genConfig = requestJson["generationConfig"]?.AsObject();
            if (genConfig != null && genConfig.ContainsKey("thinkingConfig"))
            {
                genConfig.Remove("thinkingConfig");
                logger.LogDebug("由于签名降级，已禁用 thinkingConfig");
            }
        }
    }

    /// <summary>
    /// 检测响应 body 是否为签名错误
    /// </summary>
    public static bool IsSignatureError(string? body)
    {
        // 简单判断 body 中是否包含 signature 相关的错误信息
        // 实际场景可能需要更精确的 JSON 解析，但目前字符串匹配已足够覆盖常见情况
        if (string.IsNullOrWhiteSpace(body)) return false;

        var lowerBody = body.ToLowerInvariant();
        return lowerBody.Contains("signature") ||
               lowerBody.Contains("thought_signature") ||
               (lowerBody.Contains("expected") && lowerBody.Contains("thinking"));
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
