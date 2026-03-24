# ChatModelHandler 处理器管道化重构设计方案

> 版本：v2.0 | 日期：2026-03-19 | 状态：待审查

---

## 一、现状分析

### 1.1 两个入口的当前调用链路

#### 入口 A：代理入口 `SmartReverseProxyMiddleware.InvokeAsync`

```
HttpContext
  → DownstreamRequestProcessor.ProcessAsync()        // 读取Body、Headers、提取路由
    → chatModelHandler.ExtractModelInfo()             // 提取ModelId、SessionHash
  → [选号/并发控制]
  → chatModelHandler.Configure(options)               // 注入凭证
  → InjectMetadataUserIdAsync()                       // Claude OAuth专属：注入user_id
  → chatModelHandler.TransformProtocolAsync()         // 协议转换（模型映射、格式转换）
  → chatModelHandler.ApplyProxyEnhancements()         // 代理增强（降级、签名、过滤）
  → chatModelHandler.BuildHttpRequestAsync()          // 构建UpRequestContext
  → chatModelHandler.ExecuteHttpRequestAsync()        // HTTP转发（含Fallback）
  → streamProcessor.ForwardResponseAsync()            // 响应流转发 + Token解析
```

#### 入口 B：测试入口 `AccountTokenAppService.DebugModelAsync`

```
AccountToken
  → chatModelHandlerFactory.CreateHandler(platform, token, baseUrl, ...)
  → handler.CreateDebugDownContext(modelId, message)  // 构造测试用DownContext
  → handler.TransformProtocolAsync()                  // 协议转换
  → handler.BuildHttpRequestAsync()                   // 构建UpRequestContext
  → handler.ExecuteHttpRequestAsync()                 // HTTP转发
  → streamProcessor.ParseSseStreamAsync()             // 响应流解析
```

### 1.2 当前架构的问题

| 问题 | 描述 |
|------|------|
| Handler职责过重 | `IChatModelHandler` 聚合了 5 个接口（IRequestTransformer、IRequestEnricher、IResponseParser、IErrorAnalyzer、IConnectionValidator），单个Handler文件 500-800 行 |
| 缺乏管道化机制 | Header/Body/Url 的处理逻辑散落在 `TransformProtocolAsync` 和 `BuildHttpRequestAsync` 中，无法按路由动态调整 |
| 路由感知缺失 | 当前未根据转发的路由（如 `/v1/messages` vs `/v1beta/models/:generateContent`）动态调整 Header、Body 处理策略 |
| TransformRequestContext 定位模糊 | 作为中间产物在 `TransformProtocolAsync` → `BuildHttpRequestAsync` 之间传递，但其 `ProtocolHeaders` 与 `BodyJson` 的职责与 `UpRequestContext` 重叠 |
| 两入口代码重复 | 测试入口需要手动组织 `TransformProtocolAsync → BuildHttpRequestAsync` 调用链，与代理入口的编排逻辑重复 |
| 代理增强耦合 | `ApplyProxyEnhancements` 在 Middleware 中显式调用，但其内部逻辑（如 Claude 的 thinking 降级、cache_control 限制）与协议转换紧密耦合 |

### 1.3 各平台转发逻辑差异分析

| 维度 | Claude (OAuth/ApiKey) | Gemini API (ApiKey) | Gemini Account (OAuth) | OpenAI (OAuth/ApiKey) | Antigravity |
|------|----------------------|--------------------|-----------------------|----------------------|-------------|
| **认证方式** | OAuth: `Authorization: Bearer` / ApiKey: `x-api-key` | `x-goog-api-key` | `Authorization: Bearer` | 均为 `Authorization: Bearer`，OAuth额外 `chatgpt-account-id` | `Authorization: Bearer` |
| **BaseUrl** | `api.anthropic.com` | `generativelanguage.googleapis.com` | `cloudcode-pa.googleapis.com` | OAuth: `chatgpt.com` / ApiKey: `api.openai.com` | `cloudcode-pa.googleapis.com` + Fallback: `daily-cloudcode-pa.sandbox.googleapis.com` |
| **Header处理** | 白名单过滤(17项)，Beta头动态构建，Claude Code客户端伪装 | 白名单过滤(7项)，Gemini CLI伪装 | Gemini CLI伪装，session_id/user_prompt_id注入 | OAuth: Codex专属头(session_id, x-codex-*) / ApiKey: openai-beta | Antigravity UA注入 |
| **Body处理** | OAuth: 黑名单系统提示词过滤、thinking降级、cache_control限制(max 4) / ApiKey: 无特殊处理 | JSON Schema清洗(tools参数) | 请求包装(`{model, project, request}`)，签名注入/降级 | OAuth: Chat→Responses API格式转换、参数裁剪、tool continuation检测 / ApiKey: 直透 | 请求包装(`{project, requestId, requestType, model, request}`)，签名注入/降级，requestType推断 |
| **URL处理** | 直透 RelativePath | 直透 + `?alt=sse` 强制 | 直透 | OAuth: 路由重写为 `/backend-api/codex/responses` / ApiKey: 直透 | 直透 |
| **模型映射** | 有（通过 ModelProvider） | 有 | 有 | 有 | 有（支持 Gemini + Claude 模型） |
| **Session提取** | metadata.user_id → conversation_id → cache_control → 首条消息 | Gemini CLI tmp hash → 首条消息 | Gemini CLI tmp+session_id → conversation_id → 首条消息 | session_id头 → conversation_id头 → prompt_cache_key → 首条消息 | conversation_id → 首条消息 |
| **连接验证** | 不支持（返回默认成功） | 不支持 | LoadCodeAssist → 获取project_id + 检查Tier | 不支持 | LoadCodeAssist → 获取project_id |
| **配额查询** | 不支持 | 不支持 | FetchAvailableModels → remainingFraction | 不支持 | FetchAvailableModels → remainingFraction |
| **错误分析** | 400: thinking签名错误检测 → 触发降级 | 通用(Base实现) | 400: thoughtSignature验证错误 → 两阶段降级 | 429: x-ratelimit-reset-requests解析 | Google错误格式解析(retryDelay, quotaResetDelay) |
| **Fallback** | 无 | 无 | 无 | 无 | 有（429/408/404/5xx → daily-sandbox） |

---

## 二、重构目标

1. **Handler 瘦身**：将 Handler 从"全能对象"改造为"面向业务的协调器"，内部通过 Processor 管道完成具体处理
2. **管道化处理**：引入 Processor 管道机制，每个 Processor 负责单一职责（模型映射、URL处理、Header处理、Body处理）
3. **路由感知**：Processor 可根据 `DownRequestContext.RelativePath` 动态调整处理策略
4. **两入口统一**：代理入口和测试入口共享 Handler 的核心方法，减少重复编排
5. **消除 TransformedRequestContext**：Processor 直接操作 `DownRequestContext`（读）+ `UpRequestContext`（写），无需中间载体
6. **无新增上下文对象**：`ChatModelConnectionOptions` 通过构造函数注入 Processor，不引入 PipelineContext

---

## 三、核心设计

### 3.1 整体架构

```
┌──────────────────────────────────────────────────────────────────┐
│                        调用入口层                                 │
│  ┌──────────────────────┐    ┌────────────────────────────────┐  │
│  │ SmartReverseProxy    │    │ AccountTokenAppService          │  │
│  │ Middleware            │    │ .DebugModelAsync                │  │
│  │                      │    │                                │  │
│  │ ProcessRequestContext│    │ TestChatAsync                   │  │
│  │ ProxyRequestAsync    │    │                                │  │
│  └──────────┬───────────┘    └──────────────┬─────────────────┘  │
│             │                               │                    │
│             ▼                               ▼                    │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │              IChatModelHandler (协调器)                     │   │
│  │                                                           │   │
│  │  ValidateConnectionAsync()                                │   │
│  │  FetchQuotaAsync()                                        │   │
│  │  TestChatAsync(modelId, message)     ← 测试入口           │   │
│  │  ProcessRequestContextAsync(down) → UpRequestContext       │   │
│  │  ProxyRequestAsync(upContext) → HttpResponseMessage        │   │
│  └──────────────────────┬────────────────────────────────────┘   │
│                         │                                        │
│                         ▼                                        │
│  ┌───────────────────────────────────────────────────────────┐   │
│  │         Processor Pipeline (DownContext → UpContext)        │   │
│  │                                                           │   │
│  │  ┌─────────────────┐  ┌──────────────┐  ┌─────────────┐  │   │
│  │  │ModelIdMapping   │→ │UrlProcessor  │→ │Header       │  │   │
│  │  │Processor        │  │              │  │Processor    │  │   │
│  │  └─────────────────┘  └──────────────┘  └──────┬──────┘  │   │
│  │                                                │          │   │
│  │                       ┌────────────────────────┘          │   │
│  │                       ▼                                   │   │
│  │               ┌──────────────────┐                        │   │
│  │               │RequestBody       │                        │   │
│  │               │Processor         │                        │   │
│  │               └──────────────────┘                        │   │
│  └───────────────────────────────────────────────────────────┘   │
│                                                                  │
│  ConnectionOptions 通过构造函数注入到每个 Processor               │
└──────────────────────────────────────────────────────────────────┘
```

### 3.2 UpRequestContext 改造（record → class，属性改为 set）

```csharp
namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.RequestParsing;

/// <summary>
/// 上游请求上下文（网关 → 供应商）
/// 改为可变 class，供 Processor 管道逐步填充
/// </summary>
public class UpRequestContext
{
    // HTTP Method
    public HttpMethod Method { get; set; } = HttpMethod.Post;

    // 目标信息
    public string BaseUrl { get; set; } = string.Empty;
    public string RelativePath { get; set; } = string.Empty;
    public string? QueryString { get; set; }

    public string GetFullUrl() => $"{BaseUrl}{RelativePath}{QueryString}";

    // 请求头（转换后）—— Processor 直接写入
    public Dictionary<string, string> Headers { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // 请求体（转换后）
    public string? BodyContent { get; set; }
    public HttpContent? HttpContent { get; set; }

    // 协议转换结果
    public string? MappedModelId { get; set; }
    public string? SessionId { get; set; }

    // 是否为代理模式（控制 Processor 是否执行代理增强逻辑）
    public bool IsProxyMode { get; set; }

    // 辅助方法
    public string? GetUserAgent() => Headers.TryGetValue("user-agent", out var ua) ? ua : null;
}
```

### 3.3 IRequestProcessor 接口

```csharp
namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Pipeline;

/// <summary>
/// 请求处理器接口 —— 管道中的单个处理步骤
/// 每个 Processor 负责单一职责，按顺序执行
/// Processor 从 DownRequestContext 读取原始请求信息，向 UpRequestContext 写入转换结果
/// ChatModelConnectionOptions 通过构造函数注入
/// </summary>
public interface IRequestProcessor
{
    /// <summary>
    /// 处理顺序（越小越先执行）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 执行处理逻辑
    /// </summary>
    /// <param name="down">下游请求上下文（只读，原始请求信息）</param>
    /// <param name="up">上游请求上下文（可写，Processor 逐步填充）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task ProcessAsync(
        DownRequestContext down,
        UpRequestContext up,
        CancellationToken cancellationToken = default);
}
```

### 3.4 各 Processor 职责定义

#### 3.4.1 ModelIdMappingProcessor (Order: 100)

职责：模型ID映射（`claude-sonnet-4-20250514` → 实际可用模型ID）

```csharp
/// <summary>
/// 模型ID映射处理器基类
/// 构造函数注入：ChatModelConnectionOptions、IModelProvider
/// </summary>
public abstract class ModelIdMappingProcessorBase : IRequestProcessor
{
    protected readonly ChatModelConnectionOptions Options;

    protected ModelIdMappingProcessorBase(ChatModelConnectionOptions options)
    {
        Options = options;
    }

    public int Order => 100;

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct = default)
    {
        var originalModelId = down.ModelId;
        if (string.IsNullOrEmpty(originalModelId)) return Task.CompletedTask;

        var mappedModelId = MapModel(originalModelId, down);
        up.MappedModelId = mappedModelId;

        return Task.CompletedTask;
    }

    protected abstract string MapModel(string modelId, DownRequestContext down);
}
```

各平台实现：
- `ClaudeModelIdMappingProcessor`：通过 `IModelProvider` 查找映射（构造注入 `IModelProvider`）
- `AntigravityModelIdMappingProcessor`：支持 Gemini + Claude 双模型族映射
- `OpenAiModelIdMappingProcessor`：OAuth/ApiKey 不同的模型映射规则（通过 `Options.Platform` 判断）
- `GeminiApiModelIdMappingProcessor` / `GeminiAccountModelIdMappingProcessor`

#### 3.4.2 UrlProcessor (Order: 200)

职责：目标URL的组装（BaseUrl替换、RelativePath调整、QueryString处理）

```csharp
public abstract class UrlProcessorBase : IRequestProcessor
{
    protected readonly ChatModelConnectionOptions Options;

    protected UrlProcessorBase(ChatModelConnectionOptions options)
    {
        Options = options;
    }

    public int Order => 200;

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct = default)
    {
        // 1. 解析 BaseUrl（优先使用配置的 Options.BaseUrl，否则使用平台默认值）
        up.BaseUrl = ResolveBaseUrl(down);

        // 2. 调整 RelativePath（如 OpenAI OAuth 需要重写路由）
        up.RelativePath = ResolveRelativePath(down);

        // 3. 处理 QueryString（如 Gemini 需要追加 alt=sse）
        up.QueryString = ResolveQueryString(down);

        // 4. HTTP Method（通常直透）
        up.Method = down.Method;

        return Task.CompletedTask;
    }

    protected virtual string ResolveBaseUrl(DownRequestContext down)
        => !string.IsNullOrEmpty(Options.BaseUrl) ? Options.BaseUrl : GetDefaultBaseUrl();

    protected abstract string GetDefaultBaseUrl();

    protected virtual string ResolveRelativePath(DownRequestContext down)
        => down.RelativePath;

    protected virtual string? ResolveQueryString(DownRequestContext down)
        => down.QueryString;
}
```

各平台实现要点：
- `ClaudeUrlProcessor`：直透，默认 `https://api.anthropic.com`
- `GeminiApiUrlProcessor`：追加 `?alt=sse`（流式场景），默认 `https://generativelanguage.googleapis.com`
- `GeminiAccountUrlProcessor`：直透，默认 `https://cloudcode-pa.googleapis.com`
- `OpenAiUrlProcessor`：OAuth 模式重写 RelativePath 为 `/backend-api/codex/responses`；OAuth 默认 `https://chatgpt.com`，ApiKey 默认 `https://api.openai.com`
- `AntigravityUrlProcessor`：默认 `https://cloudcode-pa.googleapis.com`

#### 3.4.3 HeaderProcessor (Order: 300)

职责：请求头的过滤、认证注入、客户端伪装

```csharp
public abstract class HeaderProcessorBase : IRequestProcessor
{
    protected readonly ChatModelConnectionOptions Options;

    protected HeaderProcessorBase(ChatModelConnectionOptions options)
    {
        Options = options;
    }

    public int Order => 300;

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct = default)
    {
        // 1. 白名单过滤（从下游 Headers 中筛选允许透传的）
        var whitelist = GetHeaderWhitelist();
        foreach (var header in down.Headers)
        {
            if (whitelist.Contains(header.Key.ToLowerInvariant()))
            {
                up.Headers[header.Key] = header.Value;
            }
        }

        // 2. 认证头注入
        ApplyAuthentication(up);

        // 3. 客户端伪装（Claude Code / Gemini CLI 等）
        if (Options.ShouldMimicOfficialClient)
        {
            ApplyClientMimicHeaders(down, up);
        }

        // 4. 平台特定头注入（如 Claude 的 anthropic-beta）
        ApplyPlatformSpecificHeaders(down, up);

        return Task.CompletedTask;
    }

    protected abstract IReadOnlySet<string> GetHeaderWhitelist();
    protected abstract void ApplyAuthentication(UpRequestContext up);
    protected virtual void ApplyClientMimicHeaders(DownRequestContext down, UpRequestContext up) { }
    protected virtual void ApplyPlatformSpecificHeaders(DownRequestContext down, UpRequestContext up) { }
}
```

各平台认证实现：
- `ClaudeHeaderProcessor`：OAuth → `Authorization: Bearer {token}` / ApiKey → `x-api-key: {token}`；动态构建 `anthropic-beta` 头；Claude Code 客户端伪装（runtime info、package version 等）
- `GeminiApiHeaderProcessor`：`x-goog-api-key: {token}`；Gemini CLI 伪装
- `GeminiAccountHeaderProcessor`：`Authorization: Bearer {token}`；Gemini CLI 伪装
- `OpenAiHeaderProcessor`：`Authorization: Bearer {token}`；OAuth 额外注入 `chatgpt-account-id`（从 `Options.ExtraProperties` 读取）
- `AntigravityHeaderProcessor`：`Authorization: Bearer {token}`；固定 `User-Agent: antigravity/1.20.5`

#### 3.4.4 RequestBodyProcessor (Order: 400)

职责：请求体的清洗、增强、格式转换、提示词注入

```csharp
public abstract class RequestBodyProcessorBase : IRequestProcessor
{
    protected readonly ChatModelConnectionOptions Options;

    protected RequestBodyProcessorBase(ChatModelConnectionOptions options)
    {
        Options = options;
    }

    public int Order => 400;

    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct = default)
    {
        if (down.IsMultipart)
        {
            // Multipart 请求直透原始字节
            up.HttpContent = new ByteArrayContent(down.BodyBytes.ToArray());
            return Task.CompletedTask;
        }

        var bodyJson = down.CloneBodyJson();
        if (bodyJson == null)
        {
            // 非 JSON body，直透原始字节
            if (down.BodyBytes.Length > 0)
            {
                up.BodyContent = down.GetBodyPreview(int.MaxValue);
                up.HttpContent = new ByteArrayContent(down.BodyBytes.ToArray());
            }
            return Task.CompletedTask;
        }

        // 1. 模型ID替换（使用映射后的ID）
        ApplyModelIdReplacement(bodyJson, down, up);

        // 2. 请求体格式转换（如 OpenAI OAuth: Chat → Responses API）
        TransformBodyFormat(bodyJson, down, up);

        // 3. 请求包装（如 Gemini Account / Antigravity 的 wrapper）
        bodyJson = WrapRequestBody(bodyJson, down, up);

        // 4. 代理增强（仅代理模式）
        if (up.IsProxyMode)
        {
            ApplyProxyEnhancements(bodyJson, down, up);
        }

        // 5. 签名处理（降级/注入）
        ApplySignatureHandling(bodyJson, down, up);

        // 6. 写入 UpContext
        var bodyContent = bodyJson.ToJsonString();
        up.BodyContent = bodyContent;
        up.HttpContent = new StringContent(bodyContent, Encoding.UTF8, "application/json");

        return Task.CompletedTask;
    }

    protected virtual void ApplyModelIdReplacement(JsonObject body, DownRequestContext down, UpRequestContext up) { }
    protected virtual void TransformBodyFormat(JsonObject body, DownRequestContext down, UpRequestContext up) { }
    protected virtual JsonObject WrapRequestBody(JsonObject body, DownRequestContext down, UpRequestContext up) => body;
    protected virtual void ApplyProxyEnhancements(JsonObject body, DownRequestContext down, UpRequestContext up) { }
    protected virtual void ApplySignatureHandling(JsonObject body, DownRequestContext down, UpRequestContext up) { }
}
```

各平台实现要点：
- `ClaudeRequestBodyProcessor`：
  - 代理增强：黑名单系统提示词过滤、thinking降级（两阶段，读取 `down.DegradationLevel`）、cache_control限制(max 4)、metadata.user_id注入
  - 模型ID替换到 body.model
- `GeminiApiRequestBodyProcessor`：JSON Schema 清洗（tools参数）
- `GeminiAccountRequestBodyProcessor`：请求包装 `{model, project, request}`（project 从 `Options.ExtraProperties` 读取）
- `OpenAiRequestBodyProcessor`：
  - OAuth: Chat Completions → Responses API 格式转换、参数裁剪、tool continuation 检测
  - ApiKey: 直透（判断 `Options.Platform`）
- `AntigravityRequestBodyProcessor`：
  - 请求包装 `{project, requestId, requestType, model, request}`
  - requestType 推断（agent/web_search/image_gen）
  - 签名注入/降级（三级：正常→移除签名→移除函数，读取 `down.DegradationLevel`）

#### 3.4.5 ResponseProcessor（非管道内，独立接口）

保持现有 `IResponseParser` + `IErrorAnalyzer` 不变，不纳入请求管道。

---

### 3.5 重构后的 IChatModelHandler 接口

```csharp
namespace AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;

/// <summary>
/// 聊天模型处理器接口（重构后）
/// 面向业务的协调器，内部通过 Processor 管道完成具体处理
/// </summary>
public interface IChatModelHandler : IResponseParser, IErrorAnalyzer
{
    /// <summary>判断是否支持指定平台</summary>
    bool Supports(ProviderPlatform platform);

    /// <summary>配置连接选项（凭证、BaseUrl等），同时重建内部 Processor 管道</summary>
    void Configure(ChatModelConnectionOptions options);

    /// <summary>
    /// 验证连接（握手/获取项目ID等）
    /// 仅需 Configure 后调用
    /// </summary>
    Task<ConnectionValidationResult> ValidateConnectionAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取账户配额信息
    /// 仅需 Configure 后调用
    /// </summary>
    Task<AccountQuotaInfo?> FetchQuotaAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 【测试入口专用】执行模型测试聊天
    /// 内部组织 DebugDownContext → 管道处理 → HTTP转发 → 返回响应
    /// </summary>
    Task<HttpResponseMessage> TestChatAsync(
        string modelId,
        string message,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 【代理入口专用】处理请求上下文
    /// 输入：DownRequestContext（由 DownstreamRequestProcessor 构建）
    /// 输出：UpRequestContext（可直接用于 ProxyRequestAsync）
    /// 内部执行完整的 Processor 管道（IsProxyMode = true）
    /// </summary>
    Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 执行 HTTP 转发请求（含 Fallback 端点重试）
    /// </summary>
    Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从下游请求中提取元信息（ModelId、SessionHash）
    /// 在 DownstreamRequestProcessor 中调用，早于管道执行
    /// </summary>
    void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId);
}
```

### 3.6 重构后的 BaseChatModelHandler

```csharp
public abstract class BaseChatModelHandler : IChatModelHandler
{
    protected readonly IHttpClientFactory HttpClientFactory;
    protected readonly SseResponseStreamProcessor StreamProcessor;
    protected readonly ISignatureCache SignatureCache;
    protected readonly UsageLoggingOptions LoggingOptions;
    protected readonly ILogger Logger;

    protected ChatModelConnectionOptions ConnectionOptions = null!;

    // Processor 管道（Configure 时重建）
    private List<IRequestProcessor> _processors = new();

    protected BaseChatModelHandler(
        IHttpClientFactory httpClientFactory,
        SseResponseStreamProcessor streamProcessor,
        ISignatureCache signatureCache,
        IOptions<UsageLoggingOptions> loggingOptions,
        ILogger logger)
    {
        HttpClientFactory = httpClientFactory;
        StreamProcessor = streamProcessor;
        SignatureCache = signatureCache;
        LoggingOptions = loggingOptions.Value;
        Logger = logger;
    }

    /// <summary>
    /// 配置连接选项，并重建 Processor 管道
    /// 每次 Configure 都会将 options 传递给子类创建新的 Processor 实例
    /// </summary>
    public virtual void Configure(ChatModelConnectionOptions options)
    {
        ConnectionOptions = options;
        _processors = CreateProcessors(options).OrderBy(p => p.Order).ToList();
    }

    /// <summary>
    /// 子类实现：根据 ConnectionOptions 创建平台专属的 Processor 列表
    /// Processor 通过构造函数接收 options 及其他依赖
    /// </summary>
    protected abstract List<IRequestProcessor> CreateProcessors(ChatModelConnectionOptions options);

    // ========== 核心业务方法 ==========

    public async Task<UpRequestContext> ProcessRequestContextAsync(
        DownRequestContext downContext,
        CancellationToken cancellationToken = default)
    {
        var upContext = new UpRequestContext
        {
            IsProxyMode = true,
            SessionId = downContext.SessionHash
        };

        foreach (var processor in _processors)
        {
            await processor.ProcessAsync(downContext, upContext, cancellationToken);
        }

        return upContext;
    }

    public async Task<HttpResponseMessage> TestChatAsync(
        string modelId,
        string message,
        CancellationToken cancellationToken = default)
    {
        // 1. 构造测试用 DownContext
        var downContext = CreateDebugDownContext(modelId, message);

        // 2. 通过管道处理（IsProxyMode = false，Processor 内部跳过代理增强）
        var upContext = new UpRequestContext
        {
            IsProxyMode = false
        };

        foreach (var processor in _processors)
        {
            await processor.ProcessAsync(downContext, upContext, cancellationToken);
        }

        // 3. 执行 HTTP 请求
        return await ProxyRequestAsync(upContext, cancellationToken);
    }

    public async Task<HttpResponseMessage> ProxyRequestAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default)
    {
        // 复用现有的 Fallback 重试逻辑
        return await ExecuteHttpRequestAsync(upContext, cancellationToken);
    }

    // ========== 保持现有抽象 ==========

    public abstract bool Supports(ProviderPlatform platform);
    public abstract Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);
    public abstract Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default);
    public abstract void ExtractModelInfo(DownRequestContext downContext, Guid apiKeyId);
    protected abstract DownRequestContext CreateDebugDownContext(string modelId, string message);

    // IResponseParser
    public abstract ChatResponsePart? ParseChunk(string chunk);
    public abstract ChatResponsePart ParseCompleteResponse(string responseBody);

    // IErrorAnalyzer（保持现有默认实现）
    public virtual Task<ModelErrorAnalysisResult> AnalyzeErrorAsync(
        int statusCode,
        Dictionary<string, IEnumerable<string>>? headers,
        string responseBody) { /* 现有逻辑不变 */ }

    // HTTP 执行（保持现有 Fallback 逻辑）
    public virtual async Task<HttpResponseMessage> ExecuteHttpRequestAsync(
        UpRequestContext upContext,
        CancellationToken cancellationToken = default) { /* 现有逻辑不变 */ }

    // BaseUrl / FallbackBaseUrl（保持现有逻辑，供 ExecuteHttpRequestAsync 使用）
    public abstract string GetDefaultBaseUrl();
    public virtual string GetBaseUrl() { /* 现有逻辑 */ }
    public virtual string? GetFallbackBaseUrl(int statusCode) => null;
}
```

### 3.7 Handler 子类示例：ClaudeChatModelHandler

```csharp
public class ClaudeChatModelHandler : BaseChatModelHandler
{
    private readonly IModelProvider _modelProvider;
    private readonly AccountFingerprintAppService _fingerprintAppService;

    public ClaudeChatModelHandler(
        IHttpClientFactory httpClientFactory,
        SseResponseStreamProcessor streamProcessor,
        ISignatureCache signatureCache,
        IOptions<UsageLoggingOptions> loggingOptions,
        IModelProvider modelProvider,
        AccountFingerprintAppService fingerprintAppService,
        ILogger<ClaudeChatModelHandler> logger)
        : base(httpClientFactory, streamProcessor, signatureCache, loggingOptions, logger)
    {
        _modelProvider = modelProvider;
        _fingerprintAppService = fingerprintAppService;
    }

    /// <summary>
    /// 每次 Configure 时创建新的 Processor 实例，注入 options 和依赖
    /// </summary>
    protected override List<IRequestProcessor> CreateProcessors(ChatModelConnectionOptions options)
    {
        return new List<IRequestProcessor>
        {
            new ClaudeModelIdMappingProcessor(options, _modelProvider),
            new ClaudeUrlProcessor(options),
            new ClaudeHeaderProcessor(options),
            new ClaudeRequestBodyProcessor(options, _fingerprintAppService, SignatureCache)
        };
    }

    // ExtractModelInfo / CreateDebugDownContext / ParseChunk 等保持现有实现
    // ValidateConnectionAsync / FetchQuotaAsync 保持现有实现
}
```

### 3.8 ChatModelHandlerFactory 调整

```csharp
public class ChatModelHandlerFactory(IServiceProvider serviceProvider) : IChatModelHandlerFactory
{
    /// <summary>
    /// 创建已配置的 Handler（Factory 调用 Configure，Configure 内部重建 Processor 管道）
    /// </summary>
    public IChatModelHandler CreateHandler(
        ProviderPlatform platform,
        string accessToken,
        string? baseUrl = null,
        Dictionary<string, string>? extraProperties = null,
        bool shouldMimicOfficialClient = true)
    {
        var options = new ChatModelConnectionOptions(
            Platform: platform,
            Credential: accessToken,
            BaseUrl: baseUrl)
        {
            ShouldMimicOfficialClient = shouldMimicOfficialClient,
            ExtraProperties = extraProperties ?? new Dictionary<string, string>()
        };

        var handler = CreateHandler(platform);
        handler.Configure(options);  // ← Configure 内部调用 CreateProcessors(options)
        return handler;
    }

    /// <summary>
    /// 创建未配置的 Handler（代理入口使用，后续由 Middleware 调用 Configure）
    /// </summary>
    public IChatModelHandler CreateHandler(ProviderPlatform platform)
    {
        var handlers = serviceProvider.GetServices<IChatModelHandler>();
        return handlers.FirstOrDefault(c => c.Supports(platform))
            ?? throw new NotFoundException($"不支持的平台类型: {platform}");
    }
}
```

> **关键时序**：代理入口中，`CreateHandler(platform)` 先返回未配置的 Handler（无 Processor），
> 选号后 `handler.Configure(options)` 时才创建 Processor 管道。这与当前 Middleware 中
> "先创建 Handler，选号后再 Configure"的时序一致。

### 3.9 重构后的两个入口调用方式

#### 入口 A：代理入口（重构后）

```csharp
// SmartReverseProxyMiddleware.InvokeAsync
public async Task InvokeAsync(HttpContext context)
{
    var (platform, apiKeyId, apiKeyName) = ValidateAndGetContext(context);
    var chatModelHandler = chatModelHandlerFactory.CreateHandler(platform);
    var downContext = await downstreamRequestProcessor.ProcessAsync(
        context, chatModelHandler, apiKeyId, context.RequestAborted);

    // ... 选号/并发控制循环 ...

    chatModelHandler.Configure(new ChatModelConnectionOptions(
        Platform: platform,
        Credential: selectResult.AccountToken.AccessToken,
        BaseUrl: selectResult.AccountToken.BaseUrl)
    {
        ShouldMimicOfficialClient = selectResult.AllowOfficialClientMimic,
        ExtraProperties = selectResult.AccountToken.ExtraProperties
    });

    downContext.DegradationLevel = degradationLevel;

    // ✅ 一步完成：管道处理（模型映射 → URL → Header → Body + 代理增强）
    var upContext = await chatModelHandler.ProcessRequestContextAsync(
        downContext, context.RequestAborted);

    // ✅ 一步完成：HTTP转发
    using var response = await chatModelHandler.ProxyRequestAsync(
        upContext, context.RequestAborted);

    // ... 响应处理/错误分析（保持现有逻辑不变）...
}
```

#### 入口 B：测试入口（重构后）

```csharp
// AccountTokenAppService.DebugModelAsync
public async IAsyncEnumerable<ChatStreamEvent> DebugModelAsync(
    Guid id,
    ChatMessageInputDto input,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var accountToken = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
        ?? throw new NotFoundException($"账户不存在: {id}");
    yield return new ChatStreamEvent(
        SystemMessage: $"开始测试 {accountToken.Platform} 平台账户 {accountToken.Name} ...");

    await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);

    var handler = chatModelHandlerFactory.CreateHandler(
        accountToken.Platform,
        accountToken.AccessToken!,
        accountToken.BaseUrl,
        accountToken.ExtraProperties,
        shouldMimicOfficialClient: true);

    // ✅ 一步完成：构造测试请求 → 管道处理 → HTTP转发
    using var response = await handler.TestChatAsync(
        input.ModelId, input.Message, cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        yield return new ChatStreamEvent(Error: $"API 错误 ({response.StatusCode}): {errorBody}");
    }

    await foreach (var evt in streamProcessor.ParseSseStreamAsync(response, handler, cancellationToken))
    {
        yield return evt;
    }
}
```

---

## 四、DownRequestContext / UpRequestContext 审视

### 4.1 DownRequestContext —— 保留，不调整

作为下游请求的快照，设计合理：

| 字段 | 说明 |
|------|------|
| `ModelId` | 由 `ExtractModelInfo` 填充，管道前就需要（用于选号路由） |
| `SessionHash` | 由 `ExtractModelInfo` 填充，管道前就需要（用于会话粘性选号） |
| `DegradationLevel` | 由 Middleware 控制，Processor 读取（`down.DegradationLevel`） |
| `BodyJsonNode` | 懒加载设计，Processor 通过 `CloneBodyJson()` 获取可修改副本 |

### 4.2 UpRequestContext —— record 改为 class + set

从不可变 record 改为可变 class，供 Processor 管道逐步填充。新增 `IsProxyMode` 字段控制代理增强行为。详见 3.2 节。

### 4.3 TransformedRequestContext —— 删除

| 原字段 | 迁移去向 |
|--------|---------|
| `BodyJson` | Processor 内部局部变量，最终序列化写入 `up.BodyContent` + `up.HttpContent` |
| `MappedModelId` | → `up.MappedModelId` |
| `ProtocolHeaders` | → `up.Headers`（各 Processor 直接写入） |

Processor 直接从 `DownRequestContext` 读、向 `UpRequestContext` 写，不需要中间载体。

---

## 五、Processor 注册与 DI 设计

### 5.1 ConnectionOptions 传递链路

```
ChatModelHandlerFactory
  → handler = CreateHandler(platform)      // DI 创建 Handler 实例
  → handler.Configure(options)             // Handler 内部调用 CreateProcessors(options)
    → new ClaudeModelIdMappingProcessor(options, modelProvider)
    → new ClaudeUrlProcessor(options)
    → new ClaudeHeaderProcessor(options)
    → new ClaudeRequestBodyProcessor(options, fingerprintService, signatureCache)
```

### 5.2 为什么不用 DI 自动注入 Processor

Processor 与 Handler 是 1:1 绑定关系（Claude 的 Processor 不能用于 Gemini），且 Processor 需要运行时的 `ChatModelConnectionOptions`（每次选号后可能不同），因此由 Handler 的 `CreateProcessors()` 显式创建。这保证了：
- **运行时绑定**：每次 `Configure` 重建 Processor，凭证变化时自动更新
- **类型安全**：编译期确认 Processor 组合
- **顺序可控**：Handler 明确声明执行顺序

### 5.3 Processor 对外部服务的依赖

部分 Processor 需要额外依赖（如 `IModelProvider`、`AccountFingerprintAppService`、`ISignatureCache`），这些依赖通过 Handler 构造函数注入，再在 `CreateProcessors()` 中传递给 Processor：

```
DI Container → Handler 构造函数 → Handler 成员字段
  → CreateProcessors(options) → new XxxProcessor(options, 依赖A, 依赖B)
```

---

## 六、关于 metadata.user_id 注入的处理

当前 `InjectMetadataUserIdAsync` 在 Middleware 中直接操作 `DownRequestContext.BodyJsonNode`，违反了 DownContext 的"只读"语义。

重构方案：迁移到 `ClaudeRequestBodyProcessor.ApplyProxyEnhancements()` 中：

```csharp
// ClaudeRequestBodyProcessor 内部
protected override void ApplyProxyEnhancements(
    JsonObject body, DownRequestContext down, UpRequestContext up)
{
    if (Options.Platform != ProviderPlatform.CLAUDE_OAUTH) return;

    // 1. 黑名单系统提示词过滤
    FilterBlacklistSystemPrompts(body);

    // 2. Thinking 降级（读取 down.DegradationLevel）
    ApplyThinkingDegradation(body, down.DegradationLevel);

    // 3. Cache control 限制
    EnforceCacheControlLimit(body, maxBlocks: 4);

    // 4. metadata.user_id 注入
    //    _fingerprintAppService 通过构造函数注入
    //    accountTokenId 和 extraProperties 从 Options.ExtraProperties 获取
    InjectMetadataUserId(body, down, Options);
}
```

> Middleware 中删除 `InjectMetadataUserIdAsync` 方法，相关的 `AccountFingerprintAppService` 依赖从 Middleware 迁移到 `ClaudeChatModelHandler` 构造函数。

---

## 七、代码目录结构调整

```
backend/src/
├── AiRelay.Domain/
│   └── Shared/ExternalServices/ChatModel/
│       ├── Handler/
│       │   ├── IChatModelHandler.cs              ← 重构：精简接口（删除继承 IRequestTransformer/IRequestEnricher）
│       │   ├── IChatModelHandlerFactory.cs       ← 保持不变
│       │   ├── IErrorAnalyzer.cs                 ← 保持不变
│       │   └── IConnectionValidator.cs           ← 保持不变
│       ├── Pipeline/                             ← 新增目录
│       │   └── IRequestProcessor.cs              ← 新增：管道处理器接口
│       ├── RequestParsing/
│       │   ├── DownRequestContext.cs              ← 保持不变
│       │   ├── UpRequestContext.cs                ← 重构：record → class + set，新增 IsProxyMode
│       │   ├── IRequestTransformer.cs            ← 删除
│       │   └── TransformedRequestContext.cs       ← 删除
│       ├── RequestEnriching/
│       │   └── IRequestEnricher.cs               ← 删除
│       └── ResponseParsing/
│           └── IResponseParser.cs                ← 保持不变
│
├── AiRelay.Infrastructure/
│   └── Shared/ExternalServices/ChatModel/
│       ├── Handler/
│       │   ├── BaseChatModelHandler.cs           ← 重构：新增 CreateProcessors() + 管道编排
│       │   ├── ClaudeChatModelHandler.cs         ← 重构：瘦身，实现 CreateProcessors()
│       │   ├── GeminiApiChatModelHandler.cs      ← 重构：瘦身
│       │   ├── GeminiAccountChatModelHandler.cs  ← 重构：瘦身
│       │   ├── OpenAiChatModelHandler.cs         ← 重构：瘦身
│       │   ├── AntigravityChatModelHandler.cs    ← 重构：瘦身
│       │   ├── GoogleInternalChatModelHandlerBase.cs ← 保持不变
│       │   └── ChatModelHandlerFactory.cs        ← 保持不变（Configure 时序不变）
│       └── Pipeline/                             ← 新增目录
│           └── Processors/
│               ├── ModelIdMapping/
│               │   ├── ModelIdMappingProcessorBase.cs
│               │   ├── ClaudeModelIdMappingProcessor.cs
│               │   ├── GeminiApiModelIdMappingProcessor.cs
│               │   ├── GeminiAccountModelIdMappingProcessor.cs
│               │   ├── OpenAiModelIdMappingProcessor.cs
│               │   └── AntigravityModelIdMappingProcessor.cs
│               ├── Url/
│               │   ├── UrlProcessorBase.cs
│               │   ├── ClaudeUrlProcessor.cs
│               │   ├── GeminiApiUrlProcessor.cs
│               │   ├── GeminiAccountUrlProcessor.cs
│               │   ├── OpenAiUrlProcessor.cs
│               │   └── AntigravityUrlProcessor.cs
│               ├── Header/
│               │   ├── HeaderProcessorBase.cs
│               │   ├── ClaudeHeaderProcessor.cs
│               │   ├── GeminiApiHeaderProcessor.cs
│               │   ├── GeminiAccountHeaderProcessor.cs
│               │   ├── OpenAiHeaderProcessor.cs
│               │   └── AntigravityHeaderProcessor.cs
│               └── RequestBody/
│                   ├── RequestBodyProcessorBase.cs
│                   ├── ClaudeRequestBodyProcessor.cs
│                   ├── GeminiApiRequestBodyProcessor.cs
│                   ├── GeminiAccountRequestBodyProcessor.cs
│                   ├── OpenAiRequestBodyProcessor.cs
│                   └── AntigravityRequestBodyProcessor.cs
│
├── AiRelay.Api/
│   └── Middleware/SmartProxy/
│       ├── SmartReverseProxyMiddleware.cs         ← 重构：简化调用链，删除 InjectMetadataUserIdAsync
│       └── RequestProcessor/
│           ├── DownstreamRequestProcessor.cs      ← 保持不变
│           └── IDownstreamRequestProcessor.cs     ← 保持不变
│
└── AiRelay.Application/
    └── ProviderAccounts/AppServices/
        └── AccountTokenAppService.cs              ← 重构：DebugModelAsync 简化为 TestChatAsync
```

---

## 八、迁移策略

### 8.1 分阶段实施

| 阶段 | 内容 | 风险 |
|------|------|------|
| Phase 1 | 新增 `Pipeline/IRequestProcessor.cs`；`UpRequestContext` 改为 class + set + 新增 `IsProxyMode` | 低：纯新增/微调 |
| Phase 2 | `BaseChatModelHandler` 新增 `CreateProcessors()` 抽象方法、`_processors` 字段、`ProcessRequestContextAsync`、`TestChatAsync`、`ProxyRequestAsync`；`Configure` 增加 `CreateProcessors` 调用 | 中：核心改动 |
| Phase 3 | 为每个平台实现 4 个 Processor（从现有 Handler 中提取逻辑），子类实现 `CreateProcessors()` | 中：逻辑迁移 |
| Phase 4 | 重构 `SmartReverseProxyMiddleware`：替换 `TransformProtocolAsync` → `ApplyProxyEnhancements` → `BuildHttpRequestAsync` 为 `ProcessRequestContextAsync`；删除 `InjectMetadataUserIdAsync` | 中：入口改动 |
| Phase 5 | 重构 `AccountTokenAppService.DebugModelAsync`：替换为 `TestChatAsync` 调用 | 低：简化调用 |
| Phase 6 | 删除 `TransformedRequestContext`、`IRequestTransformer`、`IRequestEnricher`；清理 Handler 中已迁移的方法 | 低：清理 |

### 8.2 验证要点

每个阶段完成后需验证：

1. **代理入口**：各平台（Claude OAuth/ApiKey、Gemini OAuth/ApiKey、OpenAI OAuth/ApiKey、Antigravity）的转发请求（URL、Header、Body）与重构前完全一致
2. **测试入口**：`DebugModelAsync` 的请求构造与重构前一致
3. **降级重试**：Claude thinking 降级、Antigravity 签名降级的三级机制正常工作
4. **Fallback**：Antigravity 的备用端点切换正常
5. **客户端伪装**：Claude Code / Gemini CLI 检测与伪装逻辑正常
6. **metadata 注入**：Claude OAuth 的 user_id 注入正常

---

## 九、设计决策记录

| 决策 | 选择 | 理由 |
|------|------|------|
| 是否新增 PipelineContext | **否** | Processor 直接操作 Down（读）+ Up（写），ConnectionOptions 通过构造函数注入，无需额外上下文对象 |
| UpRequestContext 类型 | record → **class + set** | Processor 管道需要逐步填充，可变 class 最直接 |
| ConnectionOptions 传递方式 | **构造函数注入 Processor** | 由 `Configure → CreateProcessors(options)` 传递，每次选号后重建 Processor 实例 |
| TransformedRequestContext | **删除** | Processor 直接写入 UpRequestContext，无需中间载体 |
| IRequestTransformer / IRequestEnricher | **删除** | 职责已分散到各 Processor |
| Processor 注册方式 | Handler 的 `CreateProcessors()` 显式创建 | 运行时绑定 options、类型安全、顺序可控 |
| ResponseProcessor 是否纳入管道 | **否** | 响应处理是独立阶段，与请求管道无关 |
| metadata.user_id 注入位置 | **迁移到 ClaudeRequestBodyProcessor** | 遵循"Body处理逻辑集中在 BodyProcessor"原则，从 Middleware 中移除 |
| Fallback URL 逻辑位置 | **保留在 BaseChatModelHandler.ExecuteHttpRequestAsync** | Fallback 是 HTTP 执行层的重试策略，不属于请求构建管道 |
| DownRequestContext 是否改为不可变 | **保持现状** | `ModelId`/`SessionHash`/`DegradationLevel` 需要外部写入 |
