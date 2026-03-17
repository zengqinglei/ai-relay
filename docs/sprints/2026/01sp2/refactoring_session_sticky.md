# 会话粘性逻辑重构方案 (Refactoring Session Sticky Logic)

## 1. 背景 (Background)

目前的会话粘性逻辑分散在 `AiRelay.Api` 层的 `ISessionStickyStrategy` 及其多个实现类中。这些策略类与 `IChatModelClient` 存在逻辑重叠（都涉及对特定平台请求体的解析）。为了进一步贯彻 **协议处理器模式**，建议将平台特定的会话提取逻辑下沉至 `IChatModelClient`，并移除 API 层冗余的策略类。

## 2. 策略 (Strategy)

*   **通用逻辑上收**：将通用的 Header 和 Query 参数提取逻辑直接集成到 `SmartReverseProxyMiddleware`（或通用 Helper）中，不再需要多态策略。
*   **特定逻辑下沉**：将从 Request Body 中提取会话 ID 的逻辑（依赖具体 JSON 结构）作为 `IChatModelClient` 的一部分。
*   **工具类共享**：将原 `BaseSessionStickyStrategy` 中的会话 ID 生成算法（哈希、过滤短消息等）提取为 `SessionIdHelper`，置于 `Domain.Shared` 层供各 Client 复用。

## 3. 调整后的树形目录结构 (Directory Structure)

```text
backend/src/
├── AiRelay.Domain.Shared/
│   └── ExternalServices/
│       └── ChatModel/
│           ├── Client/
│           │   └── IChatModelClient.cs          [修改] 新增 ExtractSessionIdFromBody 接口
│           └── Utilities/
│               └── SessionIdHelper.cs           [新增] 通用会话ID生成工具 (SHA256/长度过滤)
│
├── AiRelay.Infrastructure/
│   └── Shared/
│       └── ExternalServices/
│           └── ChatModel/
│               └── Client/
│                   ├── AntigravityChatModelClient.cs    [修改] 实现 ExtractSessionIdFromBody
│                   ├── GeminiAccountChatModelClient.cs  [修改] 同上
│                   ├── GeminiApiChatModelClient.cs      [修改] 同上
│                   ├── ClaudeChatModelClient.cs         [修改] 同上
│                   └── OpenAiChatModelClient.cs         [修改] 同上
│
└── AiRelay.Api/
    └── Middleware/
        └── SmartProxy/
            ├── SmartReverseProxyMiddleware.cs   [修改] 集成会话提取逻辑，移除 Strategy 调用
            └── SessionSticky/                   [删除] 移除整个文件夹
                ├── ISessionStickyStrategy.cs
                ├── BaseSessionStickyStrategy.cs
                ├── SessionStickyStrategyFactory.cs
                └── ... (Gemini/Claude/OpenAi Strategies)
```

## 4. 实施步骤 (Implementation Steps)

### Phase 1: 基础设施准备
1.  创建 `AiRelay.Domain.Shared...Utilities.SessionIdHelper`，迁移原 `BaseSessionStickyStrategy` 中的 `GenerateSessionId` 逻辑。
2.  修改 `IChatModelClient` 接口，增加 `string? ExtractSessionIdFromBody(string body)`。

### Phase 2: 客户端实现
在各 `ChatModelClient` 中实现接口：
*   **Antigravity/Gemini**: 解析 `conversation_id` 或哈希第一条 user message。
*   **Claude**: 解析 `conversation_id`、`metadata.user_id` 或哈希消息。
*   **OpenAI**: 解析 `prompt_cache_key`、`conversation_id` 或哈希消息。

### Phase 3: 中间件重构
修改 `SmartReverseProxyMiddleware.cs`：
1.  移除 `SessionStickyStrategyFactory` 依赖。
2.  在 `InvokeAsync` 中实现统一提取流程：
    *   检查通用 Header (`x-conversation-id` 等)。
    *   检查 Query (`session_id`)。
    *   如果未找到且是 POST 请求：读取 Body，调用 `client.ExtractSessionIdFromBody(body)`。

### Phase 4: 清理
1.  删除 `backend/src/AiRelay.Api/Middleware/SmartProxy/SessionSticky/` 目录。
2.  更新 `DependencyInjection` 和 `Program.cs` 移除相关注册。
