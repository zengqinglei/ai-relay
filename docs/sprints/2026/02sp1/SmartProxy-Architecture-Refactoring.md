# SmartProxy 架构重构方案

**日期**: 2026-02-09
**版本**: v1.0
**状态**: 待审查

---

## 📋 需求分析

### 核心目标

1. **接口职责更清晰**：将 `IChatModelClient` 拆分为多个单一职责接口
2. **消除冗余抽象**：废弃 `IUpstreamRequestBuilder`（功能被 `IRequestParser` 替代）
3. **统一流包装方式**：`SignatureExtractingStream` 改为类似 `TokenTrackingStream` 的方式
4. **Models 就近原则**：模型放在使用它的模块旁边
5. **业务上下文分离**：`UpRequestContext`/`DownRequestContext` 只包含纯技术信息，业务信息放在 `TransformContext`

---

## 🔍 可行性分析

### ✅ 优点

1. **符合 ISP（接口隔离原则）**：
   - `ProxyForwarder` 只依赖 `IRequestParser` + `IResponseParser`
   - `AccountTokenAppService` 也只依赖这两个接口
   - `IChatModelClient` 只负责平台配置和连接管理

2. **职责更清晰**：
   - `IRequestParser`：请求解析和转换
   - `IResponseParser`：响应解析
   - `IRequestSender`：HTTP 请求发送
   - `IChatModelClient`：平台配置、连接验证、配额查询

3. **上下文职责明确**：
   - `DownRequestContext`：纯 HTTP 请求信息（URL、Header、Body）
   - `UpRequestContext`：纯 HTTP 请求信息（URL、Header、Body）
   - `TransformContext`：业务信息（Platform、AccountToken、ApiKey、SessionId）

4. **Models 分散更合理**：模型和使用者在同一目录

### ⚠️ 挑战

1. **接口数量增加**：从 2 个变成 4 个
2. **实现方式选择**：方式一（拆分类）vs 方式二（一个类实现多个接口）

---

## 🎯 推荐策略

### Infrastructure Client 实现：**方式二（一个类实现多个接口）**

**理由**：

1. **本质上是同一个"平台适配器"**：
   - `ClaudeChatModelClient` 的请求解析、响应解析、连接管理都是针对 Claude 平台
   - 拆分成 3 个类会导致状态共享问题（如 `ConnectionOptions`）

2. **类数量对比**：
   - 方式一：15 个类（5平台 × 3接口）
   - 方式二：5 个类（5平台，每个实现 3 个接口）

3. **更易维护**：所有 Claude 相关逻辑在一个文件中

**实现示例**：

```csharp
public class ClaudeChatModelClient :
    IChatModelClient,      // 平台配置、连接验证
    IRequestParser,        // 请求解析和转换
    IResponseParser        // 响应解析
{
    // 所有方法在一个类中，但职责通过接口隔离
}
```

---

## 📂 调整后的目录结构

```
backend/src/AiRelay.Domain/Shared/ExternalServices/ChatModel/
├── Client/
│   ├── IChatModelClient.cs                    # 🔄 瘦身（只保留平台配置、连接管理）
│   ├── BaseChatModelClient.cs                 # 🔄 更新
│   └── IChatModelClientFactory.cs             # 不变
│
├── RequestParsing/                             # 🆕 新增目录
│   ├── IRequestParser.cs                       # 🆕 新增接口
│   ├── DownRequestContext.cs                   # 🔄 瘦身（移除业务属性）
│   └── UpRequestContext.cs                     # 🔄 瘦身（移除业务属性）
│
├── RequestSending/
│   └── IRequestSender.cs                       # 🔄 重命名（原 IHttpRequestSender）
│
├── ResponseParsing/
│   ├── IResponseParser.cs                      # 🔄 重命名（原 IChatModelResponseParser）
│   ├── ChatResponsePart.cs                     # 不变
│   └── ResponseUsage.cs                        # 不变
│
├── SignatureCache/                             # 不变
│   └── ISignatureCache.cs
│
├── Provider/                                   # 不变
│   └── IModelProvider.cs
│
└── Dto/                                        # 不变
    └── ...

backend/src/AiRelay.Infrastructure/Shared/ExternalServices/ChatModel/
├── Client/
│   ├── ClaudeChatModelClient.cs                # 🔄 实现 3 个接口
│   ├── OpenAiChatModelClient.cs                # 🔄 实现 3 个接口
│   ├── GeminiApiChatModelClient.cs             # 🔄 实现 3 个接口
│   ├── GeminiAccountChatModelClient.cs         # 🔄 实现 3 个接口
│   ├── AntigravityChatModelClient.cs           # 🔄 实现 3 个接口
│   ├── GoogleInternalChatModelClientBase.cs    # 不变
│   └── ChatModelClientFactory.cs               # 🔄 更新
│
├── RequestSending/
│   └── RequestSender.cs                        # 🔄 重命名（原 HttpRequestSender）
│
├── ResponseParsing/
│   ├── ClaudeResponseParser.cs                 # 🔄 重命名
│   ├── OpenAiResponseParser.cs                 # 🔄 重命名
│   ├── GeminiResponseParser.cs                 # 🔄 重命名
│   ├── SseResponseStreamProcessor.cs           # 🔄 更新
│   ├── SseStreamBuffer.cs                      # 不变
│   └── TokenUsageAccumulator.cs                # 不变
│
├── SignatureCache/                             # 不变
│   └── InMemorySignatureCache.cs
│
└── Provider/                                   # 不变
    └── ModelProvider.cs

backend/src/AiRelay.Api/Middleware/SmartProxy/
├── SmartReverseProxyMiddleware.cs              # 🔄 更新使用 TransformContext
│
├── Contexts/                                   # 🆕 新增目录
│   └── TransformContext.cs                     # 🆕 新增（业务上下文）
│
├── RequestProcessing/                          # 🔄 重命名（原 Processing/Downstream）
│   ├── IDownstreamRequestProcessor.cs          # 🔄 更新
│   └── DownstreamRequestProcessor.cs           # 🔄 更新
│
├── ResponseProcessing/                         # 🆕 新增目录
│   ├── TokenTrackingStream.cs                  # 📦 从 Forwarder/ 迁移
│   └── SignatureTrackingStream.cs              # 🆕 新增
│
└── Forwarder/
    ├── IProxyForwarder.cs                      # 🔄 更新签名
    ├── ProxyForwarder.cs                       # 🔄 更新使用 TransformContext
    ├── ProxyForwardResult.cs                   # 不变
    └── SimpleHttpTransformer.cs                # 不变

backend/src/AiRelay.Application/ProviderAccounts/AppServices/
└── AccountTokenAppService.cs                   # 🔄 更新使用新接口
```

---

## 🔄 删除的文件/目录

```
❌ backend/src/AiRelay.Api/Middleware/SmartProxy/Processing/Upstream/
   ├── IUpstreamRequestBuilder.cs               # 删除
   └── UpstreamRequestBuilder.cs                # 删除

❌ backend/src/AiRelay.Domain/Shared/ExternalServices/ChatModel/Models/
   # 整个目录删除（Models 分散到各模块）
```

---

## 📝 关键改动点

### 1. DownRequestContext 瘦身（移除业务属性）

```csharp
// 🔄 修改前
public class DownRequestContext
{
    public Guid RequestId { get; set; }
    public ProviderPlatform Platform { get; set; }        // ❌ 业务属性
    public Guid ApiKeyId { get; set; }                    // ❌ 业务属性
    public string ApiKeyName { get; set; }                // ❌ 业务属性

    public string Method { get; set; }                    // ✅ 技术属性
    public string Path { get; set; }                      // ✅ 技术属性
    public string? QueryString { get; set; }              // ✅ 技术属性
    public Dictionary<string, string> Headers { get; set; } // ✅ 技术属性
    public string? BodyContent { get; set; }              // ✅ 技术属性
    public JsonElement? BodyJson { get; set; }            // ✅ 技术属性

    public string? ModelId { get; set; }                  // ⚠️ 半业务（保留，用于协议转换）
    public string? SessionHash { get; set; }              // ⚠️ 半业务（保留，用于会话识别）
    public bool IsStreaming { get; set; }                 // ✅ 技术属性
}

// 🔄 修改后
public class DownRequestContext
{
    public string Method { get; set; }
    public string Path { get; set; }
    public string? QueryString { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string? BodyContent { get; set; }
    public JsonElement? BodyJson { get; set; }
    public bool IsStreaming { get; set; }

    // 协议转换需要的信息（从 Body 中提取）
    public string? ModelId { get; set; }
    public string? SessionHash { get; set; }
}
```

### 2. UpRequestContext 瘦身

```csharp
// 🔄 修改前
public class UpRequestContext
{
    public string BaseUrl { get; set; }
    public string Path { get; set; }
    public string? QueryString { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string? BodyContent { get; set; }
    public HttpContent? HttpContent { get; set; }

    public string? MappedModelId { get; set; }            // ⚠️ 半业务
    public string? SessionId { get; set; }                // ⚠️ 半业务
    public string? CapturedBody { get; set; }
    public DownRequestContext DownContext { get; set; }   // ❌ 引用了业务信息
}

// 🔄 修改后
public class UpRequestContext
{
    public string BaseUrl { get; set; }
    public string Path { get; set; }
    public string? QueryString { get; set; }
    public Dictionary<string, string> Headers { get; set; }
    public string? BodyContent { get; set; }
    public HttpContent? HttpContent { get; set; }

    // 协议转换结果
    public string? MappedModelId { get; set; }
    public string? SessionId { get; set; }
}
```

### 3. 🆕 新增 TransformContext（业务上下文）

```csharp
// 🆕 新增
namespace AiRelay.Api.Middleware.SmartProxy.Contexts;

/// <summary>
/// 请求转换上下文（包含业务信息）
/// </summary>
public class TransformContext
{
    // 业务标识
    public Guid RequestId { get; set; }
    public ProviderPlatform Platform { get; set; }
    public Guid ApiKeyId { get; set; }
    public string ApiKeyName { get; set; }
    public Guid? AccountTokenId { get; set; }
    public string? AccountTokenName { get; set; }
    public Guid? ProviderGroupId { get; set; }
    public string? ProviderGroupName { get; set; }

    // 技术上下文（引用）
    public DownRequestContext DownRequest { get; set; }
    public UpRequestContext UpRequest { get; set; }

    // 捕获的请求体（用于日志）
    public string? CapturedDownRequestBody { get; set; }
    public string? CapturedUpRequestBody { get; set; }
}
```

### 4. IChatModelClient 瘦身

```csharp
// 🔄 修改后
public interface IChatModelClient
{
    bool Supports(ProviderPlatform platform);
    string GetBaseUrl();
    string? GetFallbackBaseUrl(int statusCode);
    bool IsRetryableHttpStatus(int statusCode);
    void Configure(ChatModelConnectionOptions options);
    Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken ct = default);
    Task<AccountQuotaInfo?> FetchQuotaAsync(CancellationToken ct = default);
}
```

### 5. 🆕 新增 IRequestParser

```csharp
// 🆕 新增
public interface IRequestParser
{
    /// <summary>
    /// 从下游请求中提取信息（ModelId, SessionHash）
    /// </summary>
    void ExtractDownstreamInfo(DownRequestContext downContext);

    /// <summary>
    /// 转换下游请求为上游请求
    /// </summary>
    Task<UpRequestContext> TransformRequestAsync(
        DownRequestContext downContext,
        string baseUrl,
        CancellationToken ct = default);
}
```

### 6. 重命名接口

```csharp
// 🔄 IHttpRequestSender → IRequestSender
public interface IRequestSender
{
    Task<HttpResponseMessage> SendAsync(UpRequestContext upContext, CancellationToken ct = default);
}

// 🔄 IChatModelResponseParser → IResponseParser
public interface IResponseParser
{
    ChatResponsePart? ParseChunk(string chunk);
    ChatResponsePart ParseCompleteResponse(string responseBody);
}
```

### 7. Client 实现方式（方式二）

```csharp
public class ClaudeChatModelClient :
    BaseChatModelClient,
    IChatModelClient,
    IRequestParser,
    IResponseParser
{
    // IChatModelClient 方法（平台配置、连接管理）
    public override bool Supports(ProviderPlatform platform) { }
    public override string GetBaseUrl() { }
    public override void Configure(ChatModelConnectionOptions options) { }
    public override Task<ConnectionValidationResult> ValidateConnectionAsync(...) { }
    public override Task<AccountQuotaInfo?> FetchQuotaAsync(...) { }

    // IRequestParser 方法（请求解析和转换）
    public void ExtractDownstreamInfo(DownRequestContext downContext) { }
    public Task<UpRequestContext> TransformRequestAsync(DownRequestContext downContext, string baseUrl, ...) { }

    // IResponseParser 方法（响应解析）
    public ChatResponsePart? ParseChunk(string chunk) { }
    public ChatResponsePart ParseCompleteResponse(string responseBody) { }
}
```

### 8. ProxyForwarder 使用 TransformContext

```csharp
public async Task<ProxyForwardResult> ForwardAsync(
    HttpContext context,
    TransformContext transformContext,  // 🔄 新增参数
    CancellationToken cancellationToken)
{
    var downContext = transformContext.DownRequest;
    var upContext = transformContext.UpRequest;

    // 使用 transformContext.Platform 获取业务信息
    var client = chatModelClientFactory.CreateClient(transformContext.Platform);
    var parser = (IResponseParser)client;

    // 根据平台决定是否包装签名流
    Stream pipelineStream = context.Response.Body;
    if (transformContext.Platform == ProviderPlatform.ANTIGRAVITY)
    {
        pipelineStream = new SignatureTrackingStream(
            pipelineStream,
            upContext.SessionId,
            signatureCache);
    }

    var trackingStream = new TokenTrackingStream(
        pipelineStream,
        parser,
        downContext.IsStreaming,
        loggingOptions);

    // ...
}
```

### 9. 🆕 SignatureTrackingStream

```csharp
// 🆕 新增（类似 TokenTrackingStream）
namespace AiRelay.Api.Middleware.SmartProxy.ResponseProcessing;

public class SignatureTrackingStream : Stream
{
    private readonly Stream _innerStream;
    private readonly string? _sessionId;
    private readonly ISignatureCache _signatureCache;
    private readonly SseStreamBuffer _buffer = new();
    private bool _signatureExtracted = false;

    public SignatureTrackingStream(
        Stream innerStream,
        string? sessionId,
        ISignatureCache signatureCache)
    {
        _innerStream = innerStream;
        _sessionId = sessionId;
        _signatureCache = signatureCache;
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct = default)
    {
        if (!_signatureExtracted && !string.IsNullOrEmpty(_sessionId))
        {
            foreach (var line in _buffer.ProcessChunk(buffer.Span))
            {
                if (TryExtractSignature(line, out var signature))
                {
                    _signatureCache.CacheSignature(_sessionId, signature!);
                    _signatureExtracted = true;
                    break;
                }
            }
        }

        await _innerStream.WriteAsync(buffer, ct);
    }

    private bool TryExtractSignature(string line, out string? signature)
    {
        // 解析 SSE 行，提取 signature 字段
        // ...
    }
}
```

---

## 📊 数据流对比

### 🔄 修改前

```
SmartReverseProxyMiddleware
  ↓
DownstreamRequestProcessor → DownRequestContext (含业务属性)
  ↓
UpstreamRequestBuilder → UpRequestContext (含业务属性)
  ↓
ProxyForwarder
```

### ✅ 修改后

```
SmartReverseProxyMiddleware
  ↓
DownstreamRequestProcessor → DownRequestContext (纯技术)
  ↓
IRequestParser.TransformRequestAsync → UpRequestContext (纯技术)
  ↓
TransformContext (业务 + 技术引用)
  ↓
ProxyForwarder
```

---

## ✅ 优化效果

| 维度 | 改进 |
|------|------|
| **接口职责** | 从 1 个大接口拆分为 3 个单一职责接口 |
| **上下文分离** | 技术上下文（Down/Up）与业务上下文（Transform）分离 |
| **依赖关系** | ProxyForwarder 依赖更清晰 |
| **代码组织** | Models 就近原则 |
| **流包装统一** | SignatureTracking 和 TokenTracking 方式一致 |
| **冗余消除** | 删除 IUpstreamRequestBuilder |

---

## 📅 实施计划

### Phase 1: 接口重构（预计 2-3 小时）
1. 创建新接口和上下文
2. 重命名现有接口
3. 更新 Client 实现类

### Phase 2: 目录调整（预计 1-2 小时）
1. 创建新目录结构
2. 迁移文件
3. 删除废弃文件

### Phase 3: 业务逻辑更新（预计 2-3 小时）
1. 更新 ProxyForwarder
2. 更新 SmartReverseProxyMiddleware
3. 更新 AccountTokenAppService

### Phase 4: 测试验证（预计 1 小时）
1. 编译验证
2. 功能测试
3. 性能测试

**总计**: 6-9 小时

---

## 🎯 验收标准

- ✅ 编译无错误无警告
- ✅ 所有单元测试通过
- ✅ 接口职责清晰，符合 ISP
- ✅ 上下文分离明确
- ✅ 代码组织合理
- ✅ 性能无退化

---

**文档版本**: v1.0
**最后更新**: 2026-02-09
