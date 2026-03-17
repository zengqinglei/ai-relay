using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class GeminiApiChatModelHandler(
    GoogleJsonSchemaCleaner schemaCleaner,
    GeminiSystemPromptInjector geminiSystemPromptInjector,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger<GeminiApiChatModelHandler> logger)
    : BaseChatModelHandler(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{
    // Gemini CLI 客户端 User-Agent
    private const string GeminiCliUserAgent = "GeminiCLI/0.33.1/{0} (win32; x64) google-api-nodejs-client/10.6.1";

    // Gemini CLI 临时目录正则匹配: .gemini/tmp/[64位哈希]
    private static readonly Regex GeminiCliTmpDirRegex = new(@"\.gemini/tmp/([A-Fa-f0-9]{64})", RegexOptions.Compiled);

    // 白名单 headers
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",
        "accept-language",
        "sec-fetch-mode",
        "user-agent",
        "x-goog-api-client",
        "x-gemini-api-privileged-user-id",
        "content-type"
    };

    // ==================== IChatModelHandler ====================

    public override bool Supports(ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_APIKEY;
    }

    public override string GetDefaultBaseUrl()
    {
        return "https://generativelanguage.googleapis.com";
    }

    public override Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConnectionValidationResult(true));
    }

    public override Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<AccountQuotaInfo?>(null);
    }

    public override DownRequestContext CreateDebugDownContext(string modelId, string message)
    {
        var json = new JsonObject
        {
            ["contents"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["parts"] = new JsonArray { new JsonObject { ["text"] = message } }
                }
            }
        };

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = $"/v1beta/models/{modelId}:streamGenerateContent",
            QueryString = "?alt=sse",
            ModelId = modelId,
            BodyBytes = Encoding.UTF8.GetBytes(json.ToJsonString()).AsMemory()
        };
    }

    // ==================== IRequestTransformer ====================

    public override void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId)
    {
        // 1. 提取 ModelId - 优先从 URL 路径提取
        if (!string.IsNullOrEmpty(downContext.RelativePath) && downContext.RelativePath.Contains("/models/"))
        {
            var parts = downContext.RelativePath.Split(new[] { "/models/" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 0)
            {
                var potentialModel = parts.Last();
                var colonIndex = potentialModel.IndexOf(':');
                if (colonIndex > 0)
                {
                    downContext.ModelId = potentialModel.Substring(0, colonIndex);
                }
                else
                {
                    var slashIndex = potentialModel.IndexOf('/');
                    if (slashIndex > 0)
                    {
                        downContext.ModelId = potentialModel.Substring(0, slashIndex);
                    }
                    else
                    {
                        downContext.ModelId = potentialModel;
                    }
                }
            }
        }

        // 2. 如果路径中没有，尝试从 JSON Body 提取
        if (string.IsNullOrEmpty(downContext.ModelId) &&
            downContext.BodyJsonNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("model", out var modelProp) &&
                modelProp is JsonValue modelValue &&
                modelValue.TryGetValue<string>(out var modelId))
            {
                downContext.ModelId = modelId;
            }
        }

        // ========== 提取 SessionHash ==========
        // 优先级 1: Gemini CLI 专用逻辑 (从 tmp 目录提取)
        // 该逻辑优先级最高，即使 Header 中有 session_id 也优先使用此特征，确保 CLI 上下文正确
        var match = downContext.SearchBodyPattern(GeminiCliTmpDirRegex, maxSearchLength: 50000);
        if (match.Success && match.Groups.Count >= 2)
        {
            var tmpDirHash = match.Groups[1].Value;

            // 获取 privileged-user-id Header
            string? privilegedUserId = null;
            if (downContext.Headers != null &&
                downContext.Headers.TryGetValue("x-gemini-api-privileged-user-id", out var headerVal))
            {
                privilegedUserId = headerVal.ToString();
            }

            if (!string.IsNullOrWhiteSpace(privilegedUserId))
            {
                // 组合: privileged-user-id + ":" + tmp hash -> SHA256
                var combined = $"{privilegedUserId.Trim()}:{tmpDirHash}";
                var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(combined));
                downContext.SessionHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
            }
            else
            {
                downContext.SessionHash = tmpDirHash;
            }
            return; // 匹配成功，直接返回
        }

        if (downContext.BodyJsonNode is JsonObject root)
        {
            // 优先级 2: 只取第一条消息内容（实现会话粘性，混入上下文）
            if (root.TryGetPropertyValue("contents", out var contentsNode) &&
                contentsNode is JsonArray contents)
            {
                foreach (var contentNode in contents)
                {
                    var text = GeminiTextExtractor.ExtractTextFromParts(contentNode);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        downContext.SessionHash = GenerateSessionHashWithContext(
                            text,
                            downContext,
                            apiKeyId);
                        return;
                    }
                }
            }
        }
    }

    // ==================== IRequestEnricher ====================

    public override void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        var requestJson = transformedContext.BodyJson;
        if (requestJson == null) return;

        // 伪装逻辑
        bool shouldMimic = ConnectionOptions.ShouldMimicOfficialClient;
        bool isGeminiCliClient = IsGeminiCliClient(downContext, requestJson);

        if (shouldMimic && !isGeminiCliClient)
        {
            geminiSystemPromptInjector.InjectGeminiCliPrompt(requestJson);
        }
    }

    public override Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default)
    {
        var requestJson = downContext.CloneBodyJson();

        // 清洗 JSON Schema（tools → function_declarations/functionDeclarations → parameters）
        if (requestJson?["tools"] is JsonArray tools)
        {
            foreach (var tool in tools)
            {
                if (tool is not JsonObject toolObj) continue;

                var funcs = toolObj["function_declarations"]?.AsArray()
                         ?? toolObj["functionDeclarations"]?.AsArray();

                if (funcs != null)
                {
                    foreach (var func in funcs)
                    {
                        if (func is JsonObject funcObj && funcObj["parameters"] is JsonObject paramsObj)
                        {
                            schemaCleaner.Clean(paramsObj);
                        }
                    }
                }
            }
        }

        var transformedContext = new TransformedRequestContext
        {
            MappedModelId = downContext.ModelId, // Gemini API 不做模型映射
            BodyJson = requestJson
        };

        return Task.FromResult(transformedContext);
    }

    public override Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default)
    {
        var headers = new Dictionary<string, string>();
        var requestJson = transformedContext.BodyJson;

        // 白名单过滤下游 Headers
        foreach (var kvp in downContext.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                headers[kvp.Key] = kvp.Value;
        }

        // 透传协议特定 Headers
        foreach (var kvp in transformedContext.ProtocolHeaders)
            headers[kvp.Key] = kvp.Value;

        // 伪装逻辑
        bool shouldMimic = ConnectionOptions.ShouldMimicOfficialClient;
        bool isGeminiCliClient = requestJson != null && IsGeminiCliClient(downContext, requestJson);

        if (shouldMimic && !isGeminiCliClient && requestJson != null)
        {
            ApplyGeminiCliHeaders(headers, transformedContext.MappedModelId);
        }

        // 认证 Header
        headers["x-goog-api-key"] = ConnectionOptions.Credential;

        var relativePath = downContext.RelativePath;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/')) relativePath = "/" + relativePath;

        // 构建 QueryString（强制 SSE 格式）
        var queryString = downContext.QueryString ?? string.Empty;
        if (queryString.StartsWith("?")) queryString = queryString.Substring(1);

        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(queryString))
            queryParams.Add(queryString);
        if (!queryString.Contains("alt=sse"))
            queryParams.Add("alt=sse");

        var finalQueryString = string.Join("&", queryParams);
        if (!string.IsNullOrEmpty(finalQueryString))
            finalQueryString = "?" + finalQueryString;

        // 构建 HttpContent
        HttpContent? httpContent = null;
        string? bodyContent = null;
        if (transformedContext.BodyJson != null)
        {
            bodyContent = transformedContext.BodyJson.ToJsonString();
            httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyContent));
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8"
            };
        }

        return Task.FromResult(new UpRequestContext
        {
            Method = downContext.Method,
            BaseUrl = GetBaseUrl(),
            RelativePath = relativePath,
            QueryString = finalQueryString,
            Headers = headers,
            BodyContent = bodyContent,
            HttpContent = httpContent,
            MappedModelId = transformedContext.MappedModelId,
            SessionId = downContext.SessionHash
        });
    }

    // ==================== IResponseParser ====================

    public override ChatResponsePart? ParseChunk(string chunk)
    {
        return GeminiChatModelResponseParser.ParseChunkStatic(chunk);
    }

    public override ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        return GeminiChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
    }

    // ==================== Gemini CLI 客户端检测与伪装 ====================

    private static bool IsGeminiCliClient(DownRequestContext downContext, JsonObject? requestJson)
    {
        var userAgent = downContext.GetUserAgent();
        if (string.IsNullOrEmpty(userAgent) || !userAgent.StartsWith("GeminiCLI/", StringComparison.OrdinalIgnoreCase))
            return false;

        return !string.IsNullOrEmpty(downContext.Headers.GetValueOrDefault("x-goog-api-client")) &&
               !string.IsNullOrEmpty(downContext.Headers.GetValueOrDefault("x-gemini-api-privileged-user-id")) &&
               requestJson != null &&
               HasGeminiCliSystemPrompt(requestJson);
    }

    private static bool HasGeminiCliSystemPrompt(JsonObject requestJson)
    {
        if (!requestJson.TryGetPropertyValue("systemInstruction", out var systemNode) ||
            systemNode is not JsonObject systemObj ||
            !systemObj.TryGetPropertyValue("parts", out var partsNode) ||
            partsNode is not JsonArray parts)
            return false;

        foreach (var part in parts)
        {
            if (part is JsonObject partObj &&
                partObj.TryGetPropertyValue("text", out var textNode) &&
                textNode is JsonValue textValue &&
                textValue.TryGetValue<string>(out var text) &&
                text.Contains("Gemini CLI", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static void ApplyGeminiCliHeaders(Dictionary<string, string> headers, string? modelId)
    {
        if (!headers.ContainsKey("accept"))
            headers["accept"] = "*/*";
        if (!headers.ContainsKey("user-agent"))
            headers["user-agent"] = string.Format(GeminiCliUserAgent, modelId ?? "gemini-2.0-flash-exp");
        if (!headers.ContainsKey("x-goog-api-client"))
            headers["x-goog-api-client"] = "gl-node/22.17.0";
        if (!headers.ContainsKey("x-gemini-api-privileged-user-id"))
            headers["x-gemini-api-privileged-user-id"] = Guid.NewGuid().ToString();
        if (!headers.ContainsKey("accept-language"))
            headers["accept-language"] = "*";
        if (!headers.ContainsKey("sec-fetch-mode"))
            headers["sec-fetch-mode"] = "cors";
    }
}
