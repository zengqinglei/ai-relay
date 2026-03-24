# ChatModelHandler 重构设计方案

> Sprint: 2026/03sp1
> 状态: 待审查

---

## 一、现状分析

### 1.1 两个入口的完整链路

#### 代理入口：SmartReverseProxyMiddleware.InvokeAsync

```
HttpContext
  → DownstreamRequestProcessor.ProcessAsync(context, chatModelHandler, apiKeyId)
      └─ 内部调用 chatModelHandler.ExtractModelInfo(downContext, apiKeyId)   ← 耦合问题
  → while (accountSwitchLoop)
      → SelectAccountAsync()
      → concurrencyStrategy.AcquireSlotAsync()
      → chatModelHandler.Configure(options)                                  ← 有状态注入
      → downContext.DegradationLevel = degradationLevel                      ← 写只读对象
      → InjectMetadataUserIdAsync(downContext)                               ← 直接写 BodyJsonNode
      → chatModelHandler.TransformProtocolAsync(downContext)                 → TransformedRequestContext
      → chatModelHandler.ApplyProxyEnhancements(downContext, transformed)    ← 修改 transformed
      → chatModelHandler.BuildHttpRequestAsync(downContext, transformed)     → UpRequestContext
      → chatModelHandler.ExecuteHttpRequestAsync(upContext)                  → HttpResponseMessage
      → streamProcessor.ForwardResponseAsync(response, chatModelHandler)    ← 传入 IResponseParser
      → chatModelHandler.AnalyzeErrorAsync(statusCode, headers, body)
```

#### 测试入口：AccountTokenAppService.DebugModelAsync

```
ChatMessageInputDto
  → chatModelHandlerFactory.CreateHandler(platform, credential, baseUrl, ...)
      └─ handler.Configure(options)                                          ← 工厂内部调用
  → handler.CreateDebugDownContext(modelId, message)                         → DownRequestContext
  → handler.TransformProtocolAsync(downContext)                              → TransformedRequestContext
  → handler.BuildHttpRequestAsync(downContext, transformed)                  → UpRequestContext
  → yield SystemMessage: "测试模型 {upContext.MappedModelId}"               ← 需要 up 才能输出
  → handler.ExecuteHttpRequestAsync(upContext)                               → HttpResponseMessage
  → streamProcessor.ParseSseStreamAsync(response, handler)                  ← 传入 IResponseParser
```

### 1.2 现有问题全景

| # | 问题 | 位置 | 影响 |
|---|------|------|------|
| P1 | `DownstreamRequestProcessor` 持有 `IChatModelHandler` | `ProcessAsync` 签名 | API层耦合基础设施层，职责越界 |
| P2 | Handler 有状态：`Configure()` 写实例字段 | `BaseChatModelHandler.ConnectionOptions` | 循环内每轮 Configure，并发竞态风险 |
| P3 | `DegradationLevel` 写在 `DownRequestContext` 上 | `downContext.DegradationLevel = degradationLevel` | 下游只读请求上下文承载了重试状态机的内部状态 |
| P4 | `InjectMetadataUserIdAsync` 直接写 `BodyJsonNode` | Middleware 内部方法 | 破坏 DownRequestContext 只读语义；业务逻辑泄漏到中间件层 |
| P5 | `TransformedRequestContext` 职责混乱 | Transform → Enhance 两步共用 | 中间态对象语义不清，既是输出又被修改 |
| P6 | 路由感知缺失 | `BuildHttpRequestAsync` 硬编码 | 无法按路由动态调整 Header/Body 策略 |
| P7 | `SseResponseStreamProcessor` 依赖 `IResponseParser` | `ForwardResponseAsync(parser)` / `ParseSseStreamAsync(parser)` | 流处理器需要感知解析器，两个入口都要传 `handler` 作为 parser |
| P8 | `upContext.BodyContent` 日志字段 | `startRecord.LoggingBody(upContext.BodyContent)` | 改为可变 class 后需要保留序列化字符串用于日志 |
| P9 | `TestChatAsync` 封装破坏插入点 | 原设计方案 | Debug 入口需要在 Process 与 Proxy 之间插入系统消息 |

---

## 二、重构目标

1. **Handler 无状态化**：`ChatModelConnectionOptions` 构造函数注入（Factory 创建时传入），删除 `Configure()`
2. **解耦 DownstreamRequestProcessor**：移除 `IChatModelHandler` 参数，`ExtractModelInfo` 由 Middleware 在获得 `downContext` 后单独调用
3. **DegradationLevel 归位**：从 `DownRequestContext` 移出，作为 `ProcessRequestContextAsync` 的显式参数传入
4. **`metadata.user_id` 注入归入 Processor**：Claude OAuth 的指纹注入作为专属 Processor，在 Processor 链内处理
5. **删除 TransformedRequestContext**：Processor 链直接操作 `UpRequestContext`（改为可变 class）
6. **拆解接口聚合继承**：删除 `IRequestTransformer`、`IRequestEnricher`、`IErrorAnalyzer`、`IConnectionValidator` 子接口，方法全部直接定义在 `IChatModelHandler`
7. **`IResponseParser` 保持独立**：`SseResponseStreamProcessor` 继续依赖 `IResponseParser`（通过 Handler 实现），不纳入重构范围
8. **不提供 `TestChatAsync` 封装**：应用层自行按步骤编排，保留中间系统消息的插入能力

---

## 三、上下文对象调整

### DownRequestContext：移除 DegradationLevel

```csharp
public class DownRequestContext
{
    public HttpMethod Method { get; init; } = HttpMethod.Post;
    public string RelativePath { get; init; } = string.Empty;
    public string? QueryString { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public ReadOnlyMemory<byte> BodyBytes { get; init; }
    public bool IsMultipart { get; init; }
    public bool IsStreaming { get; ... }        // 懒加载，保留

    // 由 ExtractModelInfo 填充
    public string? ModelId { get; set; }
    public string? SessionHash { get; set; }

    // ❌ 移除：DegradationLevel 不属于下游请求，属于重试状态机
    // public int DegradationLevel { get; set; }

    public JsonNode? BodyJsonNode { get; }      // 懒加载，只读语义
    public JsonObject? CloneBodyJson() { ... }
    public string GetBodyPreview(...) { ... }
    public string? GetUserAgent() { ... }
}
```

### UpRequestContext：改为可变 class，补充 BodyContent 日志字段

```csharp
// before: record（不可变）
public record UpRequestContext { ... }

// after: class（可变，Processor 直接修改字段）
public class UpRequestContext
{
    public HttpMethod Method { get; set; } = HttpMethod.Post;

    // ── Url（UrlProcessor 填充）──────────────────────────────
    public string BaseUrl { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? QueryString { get; set; }
    public string GetFullUrl() => $"{BaseUrl}{RelativePath}{QueryString}";

    // ── Headers（HeaderProcessor 填充）──────────────────────
    public Dictionary<string, string> Headers { get; set; } = new();

    // ── Body（RequestBodyProcessor 填充）────────────────────
    public JsonObject? BodyJson { get; set; }
    public HttpContent? HttpContent { get; set; }     // multipart 场景直接传递

    /// <summary>
    /// 序列化后的 Body 字符串（用于日志捕获），由 BuildHttpRequestMessage 填充
    /// </summary>
    public string? BodyContent { get; set; }

    // ── 元数据（ModelIdMappingProcessor 填充）────────────────
    public string? MappedModelId { get; set; }
    public string? SessionId { get; set; }

    public string? GetUserAgent() =>
        Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}
```

> `BodyContent` 在 `BaseChatModelHandler.BuildHttpRequestMessage()` 内将 `BodyJson` 序列化后同步写入，Middleware 日志逻辑无需变更。

### 删除

- `TransformedRequestContext`：完全删除，无替代对象

---

## 四、IRequestProcessor 接口

```csharp
/// <summary>
/// 请求处理器：读取 down（只读），写入/修改 up（可变）
/// Processor 自身不声明 Supports 条件，由 Handler.GetProcessors(down, degradationLevel) 组合决策
/// </summary>
public interface IRequestProcessor
{
    /// <summary>执行顺序（数值越小越先执行）</summary>
    int Order { get; }

    Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct);
}
```

### Processor 执行顺序

```
Order 10: ModelIdMappingProcessor  → 模型 ID 映射，写入 up.MappedModelId
Order 20: UrlProcessor             → BaseUrl、RelativePath、QueryString
Order 30: HeaderProcessor          → 白名单过滤、认证注入、客户端伪装 Header
Order 40: RequestBodyProcessor     → Body 格式转换、清洗、协议包装、提示词注入
Order 45: MetadataInjectProcessor  → Claude OAuth 专属：fingerprint user_id 注入（异步）
Order 50: DegradationProcessor     → 降级处理（仅 degradationLevel > 0 时 Handler 加入）
```

---

## 五、IChatModelHandler 重新设计

### 删除的子接口

| 删除 | 原因 |
|------|------|
| `IRequestTransformer` | 方法归入 `IChatModelHandler` |
| `IRequestEnricher` | 职责归入 DegradationProcessor / RequestBodyProcessor |
| `IErrorAnalyzer` | 方法归入 `IChatModelHandler` |
| `IConnectionValidator` | 方法归入 `IChatModelHandler` |
| `IResponseParser` | **保留为独立接口**，`SseResponseStreamProcessor` 继续依赖它；Handler 继续实现它，但不再通过 `IChatModelHandler` 聚合 |

### 接口定义

```csharp
/// <summary>
/// 面向业务的 ChatModel 处理器接口
/// 不再聚合继承子接口，所有方法直接定义于此
/// ChatModelConnectionOptions 通过构造函数注入，Handler 实例为 Transient（单次请求作用域）
/// </summary>
public interface IChatModelHandler : IResponseParser   // 保留 IResponseParser，供 SseResponseStreamProcessor 使用
{
    bool Supports(ProviderPlatform platform);

    // ── Url 信息 ──────────────────────────────────────────────────

    /// <summary>平台默认 BaseUrl（从 _options.BaseUrl 或硬编码默认值）</summary>
    string GetBaseUrl();

    /// <summary>备用 BaseUrl（Fallback 重试）；不支持返回 null</summary>
    string? GetFallbackBaseUrl(int statusCode);

    // ── 元数据提取 ────────────────────────────────────────────────

    /// <summary>
    /// 从下游请求提取平台特定元数据（ModelId、SessionHash），填充到 down
    /// 由 Middleware 在 DownstreamRequestProcessor 之后单独调用（不再耦合到 DownstreamRequestProcessor）
    /// </summary>
    void ExtractModelInfo(DownRequestContext down, Guid apiKeyId);

    // ── 核心请求处理 ──────────────────────────────────────────────

    /// <summary>
    /// 通过 Processor 链将 DownRequestContext 转换为 UpRequestContext
    /// degradationLevel 由重试状态机维护，作为显式参数传入（不写入 DownRequestContext）
    /// Handler 在内部根据 down.RelativePath 和 degradationLevel 决策 Processor 组合
    /// </summary>
    Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default);

    /// <summary>
    /// 执行 HTTP 请求（含 Fallback BaseUrl 重试逻辑，内部封装）
    /// </summary>
    Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext up,
        CancellationToken ct = default);

    /// <summary>
    /// 分析错误响应，返回标准化重试决策
    /// </summary>
    Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody);

    // ── 测试入口专用 ──────────────────────────────────────────────

    /// <summary>
    /// 构造调试用的标准 DownRequestContext（各平台组装符合自身协议的测试 Body）
    /// 应用层调用后自行编排后续步骤，保留中间系统消息的插入能力
    /// </summary>
    DownRequestContext CreateDebugDownContext(string modelId, string message);

    // ── 账号管理 ──────────────────────────────────────────────────

    /// <summary>验证连接（从构造注入的 _options 读取凭证，无需额外参数）</summary>
    Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);

    /// <summary>获取账户配额信息；不支持的平台返回 null</summary>
    Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default);
}
```

---

## 六、ChatModelHandlerFactory 调整

Factory 注册为 Singleton，每次 `CreateHandler` 通过 `ActivatorUtilities` 创建新的 Transient Handler 实例，`options` 作为构造参数传入：

```csharp
public class ChatModelHandlerFactory(IServiceProvider serviceProvider) : IChatModelHandlerFactory
{
    private static readonly Dictionary<ProviderPlatform, Type> _handlerTypes = new()
    {
        [ProviderPlatform.CLAUDE_OAUTH]   = typeof(ClaudeChatModelHandler),
        [ProviderPlatform.CLAUDE_APIKEY]  = typeof(ClaudeChatModelHandler),
        [ProviderPlatform.OPENAI_OAUTH]   = typeof(OpenAiChatModelHandler),
        [ProviderPlatform.OPENAI_APIKEY]  = typeof(OpenAiChatModelHandler),
        [ProviderPlatform.ANTIGRAVITY]    = typeof(AntigravityChatModelHandler),
        [ProviderPlatform.GEMINI_OAUTH]   = typeof(GeminiAccountChatModelHandler),
        [ProviderPlatform.GEMINI_APIKEY]  = typeof(GeminiApiChatModelHandler),
    };

    /// <summary>
    /// 创建携带凭证的 Handler（代理入口 / 测试入口共用）
    /// options 通过构造函数注入，替代旧的 Configure()
    /// </summary>
    public IChatModelHandler CreateHandler(
        ProviderPlatform platform,
        string credential,
        string? baseUrl = null,
        Dictionary<string, string>? extraProperties = null,
        bool shouldMimicOfficialClient = true)
    {
        var options = new ChatModelConnectionOptions(
            Platform: platform,
            Credential: credential,
            BaseUrl: baseUrl)
        {
            ShouldMimicOfficialClient = shouldMimicOfficialClient,
            ExtraProperties = extraProperties ?? new()
        };

        if (!_handlerTypes.TryGetValue(platform, out var handlerType))
            throw new NotFoundException($"不支持的平台类型: {platform}");

        // ActivatorUtilities 将 options 作为构造参数注入，其余依赖从 DI 容器解析
        return (IChatModelHandler)ActivatorUtilities.CreateInstance(
            serviceProvider, handlerType, options);
    }

    /// <summary>
    /// 仅用于 Supports 路由判断（如 DownstreamRequestProcessor 确定平台类型）
    /// 不携带凭证，不执行实际请求
    /// </summary>
    public IChatModelHandler CreateHandler(ProviderPlatform platform)
    {
        if (!_handlerTypes.TryGetValue(platform, out var handlerType))
            throw new NotFoundException($"不支持的平台类型: {platform}");

        // 传入空 options 仅用于类型路由
        var emptyOptions = new ChatModelConnectionOptions(platform, string.Empty);
        return (IChatModelHandler)ActivatorUtilities.CreateInstance(
            serviceProvider, handlerType, emptyOptions);
    }
}
```

---

## 七、BaseChatModelHandler 核心实现

```csharp
public abstract class BaseChatModelHandler : IChatModelHandler
{
    protected readonly ChatModelConnectionOptions _options;
    protected readonly IHttpClientFactory _httpClientFactory;
    protected readonly SseResponseStreamProcessor _streamProcessor;
    protected readonly ISignatureCache _signatureCache;
    protected readonly ILogger _logger;

    protected BaseChatModelHandler(
        ChatModelConnectionOptions options,
        IHttpClientFactory httpClientFactory,
        SseResponseStreamProcessor streamProcessor,
        ISignatureCache signatureCache,
        ILogger logger)
    {
        _options = options;
        _httpClientFactory = httpClientFactory;
        _streamProcessor = streamProcessor;
        _signatureCache = signatureCache;
        _logger = logger;
    }

    // ── Processor 组合（子类实现，路由感知逻辑集中于此）──────────────

    /// <summary>
    /// 子类根据请求路由（down.RelativePath）和降级级别返回 Processor 列表
    /// Handler 负责"什么时候用谁"，Processor 负责"做什么"
    /// </summary>
    protected abstract IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down,
        int degradationLevel);

    // ── ProcessRequestContextAsync ─────────────────────────────────

    public async Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext down,
        int degradationLevel = 0,
        CancellationToken ct = default)
    {
        var up = new UpRequestContext { Method = down.Method };

        foreach (var processor in GetProcessors(down, degradationLevel).OrderBy(p => p.Order))
        {
            await processor.ProcessAsync(down, up, ct);
        }

        // 序列化 BodyJson → BodyContent（供日志使用）
        if (up.BodyJson != null && up.HttpContent == null)
        {
            up.BodyContent = up.BodyJson.ToJsonString();
            up.HttpContent = new StringContent(up.BodyContent, Encoding.UTF8, "application/json");
        }

        return up;
    }

    // ── ProxyRequestAsync（含 Fallback 重试，保持现有逻辑）──────────

    public async Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext up,
        CancellationToken ct = default)
    {
        var response = await ExecuteHttpAsync(up, ct);

        if (!response.IsSuccessStatusCode)
        {
            var fallback = GetFallbackBaseUrl((int)response.StatusCode);
            if (fallback != null)
            {
                _logger.LogWarning("端点异常 ({StatusCode})，切换备用端点", response.StatusCode);
                response.Dispose();
                up.BaseUrl = fallback;
                response = await ExecuteHttpAsync(up, ct);
            }
        }

        return response;
    }

    private async Task<HttpResponseMessage> ExecuteHttpAsync(UpRequestContext up, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient();
        var baseUrl = up.BaseUrl.EndsWith('/') ? up.BaseUrl : up.BaseUrl + "/";
        var relativeUrl = up.RelativePath.TrimStart('/') + (up.QueryString ?? "");
        var request = new HttpRequestMessage(up.Method, baseUrl + relativeUrl);

        foreach (var header in up.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (up.HttpContent != null)
            request.Content = up.HttpContent;

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
    }

    // ── Session Hash（保留现有实现）───────────────────────────────

    protected string GenerateSessionHashWithContext(
        string messageContent, DownRequestContext down, Guid apiKeyId) { ... }

    protected static string GenerateFallbackSessionId() { ... }
    protected static string? ExtractCacheableContent(JsonNode? root) { ... }
}
```

---

## 八、Handler 中的路由感知：GetProcessors 示例

### ClaudeChatModelHandler

```csharp
public class ClaudeChatModelHandler(
    ChatModelConnectionOptions options,
    IHttpClientFactory httpClientFactory,
    SseResponseStreamProcessor streamProcessor,
    ISignatureCache signatureCache,
    IModelProvider modelProvider,
    AccountFingerprintAppService fingerprintAppService,   // 注入用于 MetadataInjectProcessor
    ILogger<ClaudeChatModelHandler> logger)
    : BaseChatModelHandler(options, httpClientFactory, streamProcessor, signatureCache, logger)
{
    public override bool Supports(ProviderPlatform platform) =>
        platform is ProviderPlatform.CLAUDE_OAUTH or ProviderPlatform.CLAUDE_APIKEY;

    protected override IReadOnlyList<IRequestProcessor> GetProcessors(
        DownRequestContext down, int degradationLevel)
    {
        var processors = new List<IRequestProcessor>
        {
            new ClaudeModelIdMappingProcessor(modelProvider, _options),
            new ClaudeUrlProcessor(_options),
            new ClaudeRequestBodyProcessor(_options),     // 黑名单过滤、cache_control 限制
        };

        // 路由感知：OAuth / ApiKey 使用不同的 Header Processor
        processors.Add(_options.Platform == ProviderPlatform.CLAUDE_OAUTH
            ? new ClaudeOAuthHeaderProcessor(_options)
            : new ClaudeApiKeyHeaderProcessor(_options));

        // Claude OAuth 专属：fingerprint user_id 注入（异步，需要 AppService）
        // 路由感知：仅 OAuth 且非 batches 路由
        if (_options.Platform == ProviderPlatform.CLAUDE_OAUTH
            && !down.RelativePath.Contains("/batches"))
        {
            processors.Add(new ClaudeMetadataInjectProcessor(
                _options, fingerprintAppService));
        }

        // 降级 Processor 仅在需要时加入
        if (degradationLevel > 0)
            processors.Add(new ClaudeDegradationProcessor(degradationLevel));

        return processors;
    }

    // ExtractModelInfo、CreateDebugDownContext、ParseChunk、ParseCompleteResponse
    // ValidateConnectionAsync、FetchQuotaAsync、AnalyzeErrorAsync 保持现有逻辑
}
```

### AntigravityChatModelHandler

```csharp
protected override IReadOnlyList<IRequestProcessor> GetProcessors(
    DownRequestContext down, int degradationLevel)
{
    var processors = new List<IRequestProcessor>
    {
        new AntigravityModelIdMappingProcessor(modelProvider, _options),
        new AntigravityUrlProcessor(_options),
        new AntigravityHeaderProcessor(_options),
        new AntigravityRequestBodyProcessor(_options),    // v1internal 包装、身份注入、Schema 清洗
    };

    // level 1: 移除 thoughtSignature；level 2+: 移除 FunctionDeclaration
    if (degradationLevel > 0)
        processors.Add(new AntigravityDegradationProcessor(degradationLevel));

    return processors;
}
```

---

## 九、ClaudeMetadataInjectProcessor（解决 P4 问题）

将 Middleware 中的 `InjectMetadataUserIdAsync` 迁移为 Processor，归入 Processor 链：

```csharp
/// <summary>
/// Claude OAuth 专属：注入 metadata.user_id（基于账号指纹）
/// 原 Middleware.InjectMetadataUserIdAsync 迁移至此，保持现有业务逻辑不变
/// </summary>
public class ClaudeMetadataInjectProcessor(
    ChatModelConnectionOptions options,
    AccountFingerprintAppService fingerprintAppService) : IRequestProcessor
{
    public int Order => 45;  // RequestBody 之后，Degradation 之前

    public async Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        if (up.BodyJson == null) return;

        // 仅在 metadata.user_id 为空时注入（与原逻辑一致）
        if (up.BodyJson.TryGetPropertyValue("metadata", out var metadataNode) &&
            metadataNode is JsonObject metadataObj &&
            metadataObj.TryGetPropertyValue("user_id", out var userIdNode) &&
            !string.IsNullOrWhiteSpace(userIdNode?.GetValue<string>()))
        {
            return;
        }

        // 注意：此处操作 up.BodyJson（RequestBodyProcessor 已 CloneBodyJson），
        // 不再直接修改 down.BodyJsonNode，保护 DownRequestContext 只读语义
        var accountTokenId = Guid.Parse(
            options.ExtraProperties.GetValueOrDefault("account_token_id", Guid.Empty.ToString()));

        var fingerprint = await fingerprintAppService.GetOrCreateFingerprintAsync(
            accountTokenId, down.Headers, ct);

        options.ExtraProperties.TryGetValue("account_uuid", out var accountUuid);
        bool enableMasking = options.ExtraProperties.TryGetValue("session_id_masking_enabled",
            out var maskingValue) && bool.TryParse(maskingValue, out var enabled) && enabled;

        var sessionId = await fingerprintAppService.GenerateSessionUuidAsync(
            accountTokenId, down.SessionHash, enableMasking, ct);

        var userId = !string.IsNullOrWhiteSpace(accountUuid)
            ? $"user_{fingerprint.ClientId}_account_{accountUuid.Trim()}_session_{sessionId}"
            : $"user_{fingerprint.ClientId}_account__session_{sessionId}";

        if (!up.BodyJson.ContainsKey("metadata"))
            up.BodyJson["metadata"] = new JsonObject();

        if (up.BodyJson["metadata"] is JsonObject metadata)
            metadata["user_id"] = userId;
    }
}
```

> **关键变化**：原 Middleware 逻辑直接修改 `down.BodyJsonNode`（破坏只读语义），
> 新 Processor 操作 `up.BodyJson`（RequestBodyProcessor 已通过 `CloneBodyJson()` 复制），
> `DownRequestContext` 的只读性得到保护。

---

## 十、DownstreamRequestProcessor 解耦（解决 P1 问题）

移除 `IChatModelHandler` 参数，`ExtractModelInfo` 由 Middleware 在获得 `downContext` 后单独调用：

```csharp
// before
public async Task<DownRequestContext> ProcessAsync(
    HttpContext context,
    IChatModelHandler chatModelHandler,    // ← 仅为调用 ExtractModelInfo，职责越界
    Guid apiKeyId, CancellationToken ct)
{
    // ...构建 downContext...
    chatModelHandler.ExtractModelInfo(downContext, apiKeyId);   // ← 内部调用
    return downContext;
}

// after
public async Task<DownRequestContext> ProcessAsync(
    HttpContext context,
    Guid apiKeyId,
    CancellationToken ct)
{
    // ...构建 downContext，不调用任何 Handler 方法...
    return downContext;
}
```

Middleware 调用序列调整为：

```csharp
// Middleware 中
var chatModelHandler = _handlerFactory.CreateHandler(platform);   // 仅用于 ExtractModelInfo，无凭证
var downContext = await _downstreamProcessor.ProcessAsync(context, apiKeyId, ct);
chatModelHandler.ExtractModelInfo(downContext, apiKeyId);          // 单独调用，明确职责
```

> `CreateHandler(platform)`（无凭证重载）仅在此处使用，后续选号完成后再通过 `CreateHandler(platform, credential, ...)` 创建携带凭证的正式 Handler 实例。

---

## 十一、两个入口的新调用链

### 代理入口（SmartReverseProxyMiddleware）

```csharp
// 1. 解析下游请求（不再依赖 Handler）
var downContext = await _downstreamProcessor.ProcessAsync(context, apiKeyId, ct);

// 2. 提取元数据（职责明确，单独调用）
var probeHandler = _handlerFactory.CreateHandler(platform);
probeHandler.ExtractModelInfo(downContext, apiKeyId);

// 3. 外层：账号切换循环（不变）
while (true)
{
    var selectResult = await _smartProxyAppService.SelectAccountAsync(...);

    // 4. 创建携带凭证的 Handler（替代 Configure()）
    //    每次选号后创建，账号变更时自然得到新实例
    var handler = _handlerFactory.CreateHandler(
        platform,
        selectResult.AccountToken.AccessToken,
        selectResult.AccountToken.BaseUrl,
        selectResult.AccountToken.ExtraProperties,
        selectResult.AllowOfficialClientMimic);

    var degradationLevel = 0;

    // 5. 内层：同账号重试循环（不变）
    while (!shouldSwitchAccount)
    {
        await concurrencyStrategy.AcquireSlotAsync(...);

        // 6. Processor 链：degradationLevel 作为显式参数（替代写入 downContext）
        var upContext = await handler.ProcessRequestContextAsync(downContext, degradationLevel, ct);
        //    ↑ 内部已处理：metadata 注入、降级、Body 序列化为 BodyContent

        // 7. 日志记录（BodyContent 字段保留，逻辑不变）
        startRecord.LoggingBody(downContext.GetBodyPreview(...), upContext.BodyContent ?? "");
        usageRecordHostedService.TryEnqueue(startRecord);

        // 8. 执行转发（含 Fallback 重试）
        using var response = await handler.ProxyRequestAsync(upContext, ct);

        if (response.IsSuccessStatusCode)
        {
            // ForwardResponseAsync 仍传入 handler 作为 IResponseParser（保持现有逻辑）
            forwardResult = await _streamProcessor.ForwardResponseAsync(
                response, context.Response.Body, handler, downContext.IsStreaming, options, ct);
            return;
        }

        // 9. 错误分析
        var errorAnalysis = await handler.AnalyzeErrorAsync(statusCode, headers, body);

        if (errorAnalysis.RequiresDowngrade)
            degradationLevel++;   // degradationLevel 由重试状态机维护，不写 downContext
        // ...其余决策逻辑不变...
    }
}
```

### 测试入口（AccountTokenAppService.DebugModelAsync）

```csharp
// 1. 刷新 Token（不变）
await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, ct);

// 2. 创建携带凭证的 Handler（无状态，替代旧的 Configure）
var handler = _handlerFactory.CreateHandler(
    accountToken.Platform,
    accountToken.AccessToken!,
    accountToken.BaseUrl,
    accountToken.ExtraProperties,
    shouldMimicOfficialClient: true);

// 3. 构造调试请求（不变）
var downContext = handler.CreateDebugDownContext(input.ModelId, input.Message);

// 4. Processor 链（degradationLevel=0，无降级）
var upContext = await handler.ProcessRequestContextAsync(downContext, ct: ct);

// 5. 插入系统消息（需要 upContext.MappedModelId，在 Process 和 Proxy 之间插入）
var mappedModel = upContext.MappedModelId == downContext.ModelId
    ? input.ModelId
    : $"{input.ModelId} --> {upContext.MappedModelId}";
yield return new ChatStreamEvent(SystemMessage: $"测试模型 {mappedModel}");

// 6. 执行请求（含 Fallback）
using var response = await handler.ProxyRequestAsync(upContext, ct);

if (!response.IsSuccessStatusCode)
{
    var errorBody = await response.Content.ReadAsStringAsync(ct);
    yield return new ChatStreamEvent(Error: $"API 错误 ({response.StatusCode}): {errorBody}");
    yield break;
}

// 7. 解析流（传入 handler 作为 IResponseParser，保持现有逻辑）
await foreach (var evt in _streamProcessor.ParseSseStreamAsync(response, handler, ct))
    yield return evt;
```

---

## 十二、代码树形结构调整

```
backend/src/
├── AiRelay.Domain/
│   └── Shared/
│       └── ExternalServices/
│           └── ChatModel/
│               ├── Handler/
│               │   ├── IChatModelHandler.cs              ← 重新设计（直接定义所有方法，继承 IResponseParser）
│               │   └── IChatModelHandlerFactory.cs       ← 微调签名
│               ├── Processors/
│               │   └── IRequestProcessor.cs              ← 新增（Order + ProcessAsync，无 Supports）
│               └── RequestParsing/
│                   ├── DownRequestContext.cs             ← 移除 DegradationLevel
│                   ├── UpRequestContext.cs               ← record → class（可变，补充 BodyContent）
│                   └── TransformedRequestContext.cs      ← 删除
│
│   （以下文件删除）
│               ├── RequestParsing/IRequestTransformer.cs ← 删除
│               ├── RequestEnriching/IRequestEnricher.cs  ← 删除
│               ├── Handler/IErrorAnalyzer.cs             ← 删除
│               └── Handler/IConnectionValidator.cs       ← 删除
│
│   （以下文件保留）
│               └── ResponseParsing/IResponseParser.cs    ← 保留（IChatModelHandler 继承它）
│
├── AiRelay.Infrastructure/
│   └── Shared/
│       └── ExternalServices/
│           └── ChatModel/
│               ├── Handler/
│               │   ├── BaseChatModelHandler.cs                ← 重构（GetProcessors/ProcessRequestContextAsync）
│               │   ├── ClaudeChatModelHandler.cs              ← 重构（构造注入 options，GetProcessors 含路由感知）
│               │   ├── OpenAiChatModelHandler.cs              ← 重构
│               │   ├── AntigravityChatModelHandler.cs         ← 重构
│               │   ├── GeminiAccountChatModelHandler.cs       ← 重构
│               │   ├── GeminiApiChatModelHandler.cs           ← 重构
│               │   └── ChatModelHandlerFactory.cs             ← 调整（ActivatorUtilities 传入 options）
│               └── Processors/
│                   ├── Claude/
│                   │   ├── ClaudeModelIdMappingProcessor.cs
│                   │   ├── ClaudeUrlProcessor.cs
│                   │   ├── ClaudeOAuthHeaderProcessor.cs
│                   │   ├── ClaudeApiKeyHeaderProcessor.cs
│                   │   ├── ClaudeRequestBodyProcessor.cs       ← 黑名单过滤、cache_control 限制
│                   │   ├── ClaudeMetadataInjectProcessor.cs    ← 新增（迁移自 Middleware.InjectMetadataUserIdAsync）
│                   │   └── ClaudeDegradationProcessor.cs       ← Thinking block 降级
│                   ├── OpenAi/
│                   │   ├── OpenAiModelIdMappingProcessor.cs
│                   │   ├── OpenAiOAuthUrlProcessor.cs
│                   │   ├── OpenAiApiKeyUrlProcessor.cs
│                   │   ├── OpenAiOAuthHeaderProcessor.cs
│                   │   ├── OpenAiApiKeyHeaderProcessor.cs
│                   │   └── OpenAiOAuthRequestBodyProcessor.cs  ← Chat Completions → Responses API 转换
│                   ├── Antigravity/
│                   │   ├── AntigravityModelIdMappingProcessor.cs
│                   │   ├── AntigravityUrlProcessor.cs
│                   │   ├── AntigravityHeaderProcessor.cs
│                   │   ├── AntigravityRequestBodyProcessor.cs  ← v1internal 包装、身份注入、Schema 清洗
│                   │   └── AntigravityDegradationProcessor.cs  ← thoughtSignature / FunctionDeclaration 移除
│                   └── Gemini/
│                       ├── GeminiModelIdMappingProcessor.cs
│                       ├── GeminiOAuthUrlProcessor.cs
│                       ├── GeminiApiKeyUrlProcessor.cs
│                       ├── GeminiOAuthHeaderProcessor.cs
│                       ├── GeminiApiKeyHeaderProcessor.cs
│                       ├── GeminiOAuthRequestBodyProcessor.cs  ← v1internal 包装、CLI metadata 注入
│                       ├── GeminiApiKeyRequestBodyProcessor.cs ← JsonSchema 清洗
│                       └── GeminiDegradationProcessor.cs       ← thoughtSignature / FunctionDeclaration 移除
│
├── AiRelay.Api/
│   └── Middleware/SmartProxy/
│       ├── SmartReverseProxyMiddleware.cs              ← 调整调用链（移除 InjectMetadataUserIdAsync，degradationLevel 显式传参）
│       └── RequestProcessor/
│           ├── DownstreamRequestProcessor.cs           ← 移除 IChatModelHandler 参数
│           └── IDownstreamRequestProcessor.cs          ← 更新签名
│
└── AiRelay.Application/
    └── ProviderAccounts/AppServices/
        └── AccountTokenAppService.cs                   ← DebugModelAsync 按步骤调用（保留系统消息插入点）
```

---

## 十三、问题对照表（改造前后）

| # | 问题 | 改造前 | 改造后 |
|---|------|--------|--------|
| P1 | DownstreamRequestProcessor 耦合 Handler | 参数含 `IChatModelHandler` | 移除参数，Middleware 单独调用 `ExtractModelInfo` |
| P2 | Handler 有状态 | `Configure()` 写实例字段 | 构造注入 options，Handler 为 Transient |
| P3 | DegradationLevel 写 DownRequestContext | `downContext.DegradationLevel = level` | 作为 `ProcessRequestContextAsync(degradationLevel)` 显式参数 |
| P4 | InjectMetadataUserIdAsync 写 BodyJsonNode | Middleware 直接修改 down | 迁移为 `ClaudeMetadataInjectProcessor`，操作 `up.BodyJson` |
| P5 | TransformedRequestContext 职责混乱 | Transform → Enhance 两步共用 | 删除，Processor 链直接操作 `UpRequestContext` |
| P6 | 路由感知缺失 | BuildHttpRequestAsync 硬编码 | `GetProcessors(down, level)` 按路由组合 Processor |
| P7 | SseResponseStreamProcessor 依赖 IResponseParser | 传入 handler 作为 parser | 保持现有方式（`IChatModelHandler : IResponseParser`），不改动 |
| P8 | BodyContent 日志字段 | `UpRequestContext.BodyContent` | 保留该字段，`ProcessRequestContextAsync` 末尾序列化写入 |
| P9 | TestChatAsync 破坏系统消息插入点 | 原设计方案过度封装 | 不提供 `TestChatAsync`，应用层自行按步骤编排 |

---

## 十四、迁移策略

| 阶段 | 内容 | 验证 |
|------|------|------|
| 阶段一 | 新增 `IRequestProcessor`；`UpRequestContext` 改为 class（补充 BodyContent）；`DownRequestContext` 移除 `DegradationLevel` | 编译通过，现有测试不变 |
| 阶段二 | 逐平台实现 Processor；`BaseChatModelHandler` 新增 `ProcessRequestContextAsync`；`ChatModelHandlerFactory` 切换为 ActivatorUtilities | 单元测试覆盖各 Processor |
| 阶段三 | `DownstreamRequestProcessor` 移除 Handler 参数；Middleware 调整调用链；`DebugModelAsync` 按步骤调用 | E2E 验证各平台转发行为与改造前一致 |
| 阶段四 | 删除旧方法：`TransformProtocolAsync`、`ApplyProxyEnhancements`、`BuildHttpRequestAsync`、`Configure()`；删除旧文件：`TransformedRequestContext`、4个子接口文件 | 编译清洁，无残留 |
