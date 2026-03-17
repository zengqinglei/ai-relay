using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Cleaning;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Parsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.ResponseParsing;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.Parsers;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.SignatureCache;
using AiRelay.Domain.UsageRecords.Options;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.Handler;

public class OpenAiChatModelHandler(
    OpenAiCodexInjector openAiCodexInjector,
    IModelProvider modelProvider,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IOptions<UsageLoggingOptions> loggingOptions,
    ILogger<OpenAiChatModelHandler> logger)
    : BaseChatModelHandler(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
{

    // OpenAI 白名单 Headers
    // 注意：不包含 content-type，因为它由 HttpContent 自动管理
    private static readonly HashSet<string> AllowedHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "accept",              // 允许客户端指定响应格式（API Key 模式需要）
        "accept-language",
        "conversation_id",
        "user-agent",
        "originator",
        "session_id",
        "x-codex-turn-state",
        "x-codex-turn-metadata",
        "openai-beta"          // 允许客户端启用 beta 功能（API Key 模式需要）
    };

    // ==================== IChatModelHandler ====================

    public override bool Supports(ProviderPlatform platform)
    {
        return platform is ProviderPlatform.OPENAI_OAUTH or ProviderPlatform.OPENAI_APIKEY;
    }

    public override string GetDefaultBaseUrl()
    {
        // 根据 Platform 返回不同的默认域名（不包含路径）
        return ConnectionOptions.Platform switch
        {
            ProviderPlatform.OPENAI_OAUTH => "https://chatgpt.com",
            _ => "https://api.openai.com" // API KEY
        };
    }

    public override async Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody)
    {
        // 429 Too Many Requests
        if (statusCode == 429)
        {
            var result = new ModelErrorAnalysisResult
            {
                ErrorType = ModelErrorType.RateLimit,
                IsRetryableOnSameAccount = false,
                RequiresDowngrade = false,
                RetryAfter = null
            };

            // 1. 尝试解析 x-ratelimit-reset-requests (格式如: 20ms, 1s, 1m)
            if (headers != null && headers.TryGetValue("x-ratelimit-reset-requests", out var resetValues))
            {
                var resetStr = resetValues.FirstOrDefault();
                if (!string.IsNullOrEmpty(resetStr))
                {
                    result.RetryAfter = OpenAiDurationParser.ParseDurationString(resetStr);
                }
            }

            // 2. 尝试解析 Retry-After (秒数或 HTTP Date) - 使用基类通用逻辑
            if (result.RetryAfter == null)
            {
                result.RetryAfter = ExtractRetryAfterGeneric(headers, responseBody);
            }

            return result;
        }

        return await base.AnalyzeErrorAsync(statusCode, headers, responseBody);
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
        // 使用 OpenAI Responses API 格式（而非 Chat Completions API）
        // 参考 sub2api: account_test_service.go createOpenAITestPayload
        var json = new JsonObject
        {
            ["model"] = modelId,
            ["input"] = new JsonArray
            {
                new JsonObject
                {
                    ["role"] = "user",
                    ["content"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["type"] = "input_text",
                            ["text"] = message
                        }
                    }
                }
            },
            ["stream"] = true,
            // All accounts require instructions for Responses API
            ["instructions"] = "You are a helpful AI assistant."
        };

        // OAuth 模式需要 store: false
        if (ConnectionOptions.Platform == ProviderPlatform.OPENAI_OAUTH)
        {
            json["store"] = false;
        }

        // 两种模式使用不同的路径
        // OAuth: /backend-api/codex/responses
        // API Key: /v1/responses
        string path = ConnectionOptions.Platform == ProviderPlatform.OPENAI_OAUTH
            ? "/backend-api/codex/responses"
            : "/v1/responses";

        return new DownRequestContext
        {
            Method = HttpMethod.Post,
            RelativePath = path,
            ModelId = modelId,
            BodyBytes = Encoding.UTF8.GetBytes(json.ToJsonString()).AsMemory()
        };
    }

    // ==================== IRequestTransformer ====================

    public override Task<TransformedRequestContext> TransformProtocolAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default)
    {
        bool isOAuthMode = ConnectionOptions.Platform == ProviderPlatform.OPENAI_OAUTH;

        var mappedModelId = string.IsNullOrEmpty(downContext.ModelId)
            ? "gpt-4o"
            : modelProvider.GetOpenAIMappedModel(downContext.ModelId);

        JsonObject? finalBodyJson;

        if (!isOAuthMode)
        {
            // API Key 模式：透传白名单 Headers，不需要 body 转换
            finalBodyJson = downContext.CloneBodyJson();
        }
        else
        {
            // OAuth 模式：协议转换
            var requestJson = downContext.CloneBodyJson();
            if (requestJson == null)
            {
                finalBodyJson = null;
            }
            else
            {
                // 1. 模型映射并修改 body
                if (mappedModelId != downContext.ModelId)
                    requestJson["model"] = mappedModelId;

                // 2. reasoning.effort 修正 (minimal -> none)
                if (requestJson.TryGetPropertyValue("reasoning", out var reasoningNode) && reasoningNode is JsonObject reasoningObj)
                {
                    if (reasoningObj.TryGetPropertyValue("effort", out var effortNode) && effortNode != null)
                    {
                        var effort = effortNode.GetValue<string>();
                        if (effort == "minimal")
                            reasoningObj["effort"] = "none";
                    }
                }

                // 3. 移除 Codex API 不支持的参数
                var unsupportedParams = new[]
                {
                    "max_output_tokens", "max_completion_tokens", "temperature",
                    "top_p", "frequency_penalty", "presence_penalty"
                };
                foreach (var param in unsupportedParams)
                    requestJson.Remove(param);

                // 4. 工具规范化（Chat Completions -> Responses API）
                NormalizeCodexTools(requestJson);

                // 5. Input 过滤
                bool needsToolContinuation = NeedsToolContinuation(requestJson);
                FilterCodexInput(requestJson, needsToolContinuation);

                // 6. OAuth 模式必需字段
                requestJson["store"] = false;
                requestJson["stream"] = true;

                finalBodyJson = requestJson;
            }
        }

        var transformedContext = new TransformedRequestContext
        {
            MappedModelId = mappedModelId,
            BodyJson = finalBodyJson
        };

        return Task.FromResult(transformedContext);
    }

    // ==================== IRequestEnricher ====================

    public override void ApplyProxyEnhancements(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        bool isOAuthMode = ConnectionOptions.Platform == ProviderPlatform.OPENAI_OAUTH;
        if (!isOAuthMode) return; // API Key 模式无代理增强

        var requestJson = transformedContext.BodyJson;
        if (requestJson == null) return;

        // Instructions 注入（Codex 模式）
        openAiCodexInjector.InjectCodexInstructions(requestJson, downContext.GetUserAgent());
    }

    // ==================== IRequestTransformer ====================

    public override void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId)
    {
        // 提取 ModelId
        if (downContext.BodyJsonNode is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("model", out var modelProp) &&
                modelProp is JsonValue modelValue &&
                modelValue.TryGetValue<string>(out var modelId))
            {
                downContext.ModelId = modelId;
            }
        }

        // ========== 提取 SessionHash ==========
        // 优先级: Header > Body

        // 优先级 1: Header session_id（最高优先级）
        if (downContext.Headers.TryGetValue("session_id", out var sessionIdHeader))
        {
            var sessionId = sessionIdHeader.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(sessionId))
            {
                downContext.SessionHash = sessionId;
                return;
            }
        }

        // 优先级 2: Header conversation_id
        if (downContext.Headers.TryGetValue("conversation_id", out var conversationIdHeader))
        {
            var conversationId = conversationIdHeader.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(conversationId))
            {
                downContext.SessionHash = conversationId;
                return;
            }
        }

        // 优先级 3+: 从 Body 提取
        if (downContext.BodyJsonNode is JsonObject root)
        {
            // 优先级 3: prompt_cache_key（直接使用，不混入上下文）
            if (root.TryGetPropertyValue("prompt_cache_key", out var cacheKeyNode) &&
                cacheKeyNode is JsonValue cacheKeyValue &&
                cacheKeyValue.TryGetValue<string>(out var key))
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    downContext.SessionHash = key;
                    return;
                }
            }

            // 优先级 4: conversation_id（直接使用，不混入上下文）
            if (root.TryGetPropertyValue("conversation_id", out var convIdNode) &&
                convIdNode is JsonValue convIdValue &&
                convIdValue.TryGetValue<string>(out var id))
            {
                if (!string.IsNullOrWhiteSpace(id))
                {
                    downContext.SessionHash = id;
                    return;
                }
            }

            // 优先级 5: 只取第一条消息内容（实现会话粘性，混入上下文）
            if (root.TryGetPropertyValue("messages", out var messagesNode) &&
                messagesNode is JsonArray messages)
            {
                foreach (var messageNode in messages)
                {
                    var text = ExtractTextFromMessage(messageNode);
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

    private static string ExtractTextFromMessage(JsonNode? message)
    {
        if (message is not JsonObject messageObj ||
            !messageObj.TryGetPropertyValue("content", out var contentNode))
            return string.Empty;

        if (contentNode is JsonValue contentValue &&
            contentValue.TryGetValue<string>(out var contentStr))
        {
            return contentStr ?? string.Empty;
        }

        if (contentNode is JsonArray contentArray)
        {
            var sb = new StringBuilder();
            foreach (var part in contentArray)
            {
                if (part is JsonObject partObj &&
                    partObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue &&
                    typeValue.TryGetValue<string>(out var type) &&
                    type == "text" &&
                    partObj.TryGetPropertyValue("text", out var textNode) &&
                    textNode is JsonValue textValue &&
                    textValue.TryGetValue<string>(out var text))
                {
                    sb.Append(text);
                }
            }
            return sb.ToString();
        }

        return string.Empty;
    }

    public override Task<UpRequestContext> BuildHttpRequestAsync(
        DownRequestContext downContext,
        TransformedRequestContext transformedContext,
        CancellationToken cancellationToken = default)
    {
        bool isOAuthMode = ConnectionOptions.Platform == ProviderPlatform.OPENAI_OAUTH;
        return isOAuthMode
            ? BuildOAuthHttpRequestAsync(downContext, transformedContext)
            : BuildApiKeyHttpRequestAsync(downContext, transformedContext);
    }

    /// <summary>
    /// 构建 OAuth 模式请求（ChatGPT Codex API）
    /// </summary>
    private Task<UpRequestContext> BuildOAuthHttpRequestAsync(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        var relativePath = downContext.RelativePath;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/')) relativePath = "/" + relativePath;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["authorization"] = $"Bearer {ConnectionOptions.Credential}",
            ["Host"] = "chatgpt.com"
        };

        // 白名单过滤下游 Headers
        foreach (var kvp in downContext.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                headers[kvp.Key] = kvp.Value;
        }

        // 透传协议特定 Headers
        foreach (var kvp in transformedContext.ProtocolHeaders)
            headers[kvp.Key] = kvp.Value;

        // OAuth 必需 Headers
        headers["OpenAI-Beta"] = "responses=experimental";
        headers["accept"] = "text/event-stream";

        bool isCodexCLI = OpenAiCodexInjector.IsCodexCLIRequest(downContext.GetUserAgent());
        headers["originator"] = isCodexCLI ? "codex_cli_rs" : "opencode";

        if (ConnectionOptions.ExtraProperties.TryGetValue("chatgpt_account_id", out var accountId)
            && !string.IsNullOrWhiteSpace(accountId))
        {
            headers["chatgpt-account-id"] = accountId;
        }

        if (!string.IsNullOrEmpty(downContext.SessionHash))
        {
            headers["conversation_id"] = downContext.SessionHash;
            headers["session_id"] = downContext.SessionHash;
        }

        HttpContent? httpContent = null;
        string? bodyContent = null;
        if (transformedContext.BodyJson != null)
        {
            bodyContent = transformedContext.BodyJson.ToJsonString();
            httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyContent));
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return Task.FromResult(new UpRequestContext
        {
            Method = downContext.Method,
            BaseUrl = GetBaseUrl(),
            RelativePath = relativePath,
            QueryString = downContext.QueryString,
            Headers = headers,
            BodyContent = bodyContent,
            HttpContent = httpContent,
            MappedModelId = transformedContext.MappedModelId,
            SessionId = downContext.SessionHash
        });
    }

    /// <summary>
    /// 构建 API Key 模式请求（OpenAI Platform API）
    /// </summary>
    private Task<UpRequestContext> BuildApiKeyHttpRequestAsync(DownRequestContext downContext, TransformedRequestContext transformedContext)
    {
        var relativePath = downContext.RelativePath;
        if (!string.IsNullOrEmpty(relativePath) && !relativePath.StartsWith('/')) relativePath = "/" + relativePath;

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 白名单过滤下游 Headers
        foreach (var kvp in downContext.Headers)
        {
            if (AllowedHeaders.Contains(kvp.Key) && !string.IsNullOrEmpty(kvp.Value))
                headers[kvp.Key] = kvp.Value;
        }

        // 透传协议特定 Headers
        foreach (var kvp in transformedContext.ProtocolHeaders)
            headers[kvp.Key] = kvp.Value;

        // 清理可能存在的认证头
        headers.Remove("x-api-key");
        headers.Remove("x-goog-api-key");
        headers.Remove("cookie");

        headers["authorization"] = $"Bearer {ConnectionOptions.Credential}";

        HttpContent? httpContent = null;
        string? bodyContent = null;
        if (transformedContext.BodyJson != null)
        {
            bodyContent = transformedContext.BodyJson.ToJsonString();
            httpContent = new ByteArrayContent(Encoding.UTF8.GetBytes(bodyContent));
            httpContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        return Task.FromResult(new UpRequestContext
        {
            Method = downContext.Method,
            BaseUrl = GetBaseUrl(),
            RelativePath = relativePath,
            QueryString = downContext.QueryString,
            Headers = headers,
            BodyContent = bodyContent,
            HttpContent = httpContent,
            MappedModelId = transformedContext.MappedModelId,
            SessionId = downContext.SessionHash
        });
    }

    /// <summary>
    /// 透传白名单 Headers
    /// </summary>
    private static void PassthroughAllowedHeaders(DownRequestContext downContext, Dictionary<string, string> headers)
    {
        if (downContext.Headers == null) return;

        foreach (var header in downContext.Headers)
        {
            if (AllowedHeaders.Contains(header.Key))
            {
                var value = header.Value.ToString();
                if (!string.IsNullOrEmpty(value))
                {
                    headers[header.Key] = value;
                }
            }
        }
    }


    /// <summary>
    /// 判断请求是否需要工具调用续链处理
    /// 满足以下任一条件即视为续链：
    /// - 存在 previous_response_id
    /// - 存在 tools 或 tool_choice
    /// - input 中包含 function_call_output 或 item_reference
    /// </summary>
    private static bool NeedsToolContinuation(JsonObject jsonNode)
    {
        // 检查 previous_response_id
        if (jsonNode.TryGetPropertyValue("previous_response_id", out var prevId) &&
            prevId is JsonValue prevIdValue &&
            !string.IsNullOrWhiteSpace(prevIdValue.GetValue<string>()))
        {
            return true;
        }

        // 检查 tools
        if (jsonNode.TryGetPropertyValue("tools", out var tools) && tools is JsonArray toolsArray && toolsArray.Count > 0)
        {
            return true;
        }

        // 检查 tool_choice
        if (jsonNode.TryGetPropertyValue("tool_choice", out var toolChoice) && toolChoice != null)
        {
            return true;
        }

        // 检查 input 中是否包含 function_call_output 或 item_reference
        if (jsonNode.TryGetPropertyValue("input", out var inputNode) && inputNode is JsonArray input)
        {
            foreach (var item in input)
            {
                if (item is JsonObject itemObj &&
                    itemObj.TryGetPropertyValue("type", out var typeNode) &&
                    typeNode is JsonValue typeValue)
                {
                    var type = typeValue.GetValue<string>();
                    if (type == "function_call_output" || type == "item_reference")
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 过滤 input 数组
    /// </summary>
    /// <param name="jsonNode">请求 JSON 对象</param>
    /// <param name="preserveReferences">是否保留引用字段（工具续链场景需要保留）</param>
    private static void FilterCodexInput(JsonObject jsonNode, bool preserveReferences)
    {
        if (!jsonNode.TryGetPropertyValue("input", out var inputNode) || inputNode is not JsonArray input)
            return;

        for (int i = input.Count - 1; i >= 0; i--)
        {
            if (input[i] is not JsonObject item) continue;

            var type = item.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;

            // 处理 item_reference
            if (type == "item_reference")
            {
                if (!preserveReferences)
                {
                    input.RemoveAt(i);  // 非续链场景：移除
                    continue;
                }
                // 续链场景：保留 item_reference
                continue;
            }

            // 为工具调用补充 call_id
            if (IsCodexToolCallItemType(type) &&
                (!item.TryGetPropertyValue("call_id", out var callId) || string.IsNullOrWhiteSpace(callId?.GetValue<string>())) &&
                item.TryGetPropertyValue("id", out var id) && !string.IsNullOrWhiteSpace(id?.GetValue<string>()))
            {
                item["call_id"] = id.DeepClone();
            }

            // 移除 id 和 call_id（根据场景决定）
            if (!preserveReferences)
            {
                item.Remove("id");
                if (!IsCodexToolCallItemType(type))
                {
                    item.Remove("call_id");
                }
            }
            // 续链场景：保留 id 和 call_id
        }
    }


    private static bool IsCodexToolCallItemType(string? type)
    {
        return type != null && (type.EndsWith("_call") || type.EndsWith("_call_output"));
    }

    /// <summary>
    /// 规范化工具定义（Chat Completions -> Responses API）
    /// </summary>
    private static void NormalizeCodexTools(JsonObject jsonNode)
    {
        if (!jsonNode.TryGetPropertyValue("tools", out var toolsNode) || toolsNode is not JsonArray tools)
            return;

        for (int i = tools.Count - 1; i >= 0; i--)
        {
            if (tools[i] is not JsonObject tool) continue;

            var toolType = tool.TryGetPropertyValue("type", out var t) ? t?.GetValue<string>() : null;
            if (toolType != "function") continue;

            // Responses API 格式：顶层有 name
            if (tool.TryGetPropertyValue("name", out var nameNode) &&
                nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetValue<string>()))
                continue;

            // Chat Completions 格式：{type:"function", function:{name, description, parameters}}
            if (!tool.TryGetPropertyValue("function", out var funcNode) || funcNode is not JsonObject func)
            {
                tools.RemoveAt(i);
                continue;
            }

            // 提升字段到顶层
            if (func.TryGetPropertyValue("name", out var n) && n != null)
                tool["name"] = n.DeepClone();
            if (func.TryGetPropertyValue("description", out var d) && d != null)
                tool["description"] = d.DeepClone();
            if (func.TryGetPropertyValue("parameters", out var p) && p != null)
                tool["parameters"] = p.DeepClone();
            if (func.TryGetPropertyValue("strict", out var s) && s != null)
                tool["strict"] = s.DeepClone();
        }
    }

    // ==================== IResponseParser ====================

    public override ChatResponsePart? ParseChunk(string chunk)
    {
        return OpenAiChatModelResponseParser.ParseChunkStatic(chunk);
    }

    public override ChatResponsePart ParseCompleteResponse(string responseBody)
    {
        return OpenAiChatModelResponseParser.ParseCompleteResponseStatic(responseBody);
    }
}
