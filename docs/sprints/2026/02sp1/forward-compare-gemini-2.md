# Sub2API (Go) 与 AiRelay (C#) 请求转发对比报告

**日期:** 2026-02-13
**范围:** Gemini, Claude, OpenAI, Antigravity
**重点:** 请求转发逻辑、账号选择、会话处理以及协议转换。

## 1. 架构概览 (Architectural Overview)

| 特性 | `sub2api` (Go) | `ai-relay` (C#) |
| :--- | :--- | :--- |
| **入口点** | 特定 Handler (如 `gemini_v1beta_handler.go`) | 通用 `SmartReverseProxyMiddleware` + `ProxyForwarder` |
| **路由** | 每个端点组显式路由 (Gin) | 基于元数据的通配符路由 (`/{platform}/{**catch-all}`) |
| **转发引擎** | 自定义 `http.Client` 逻辑 | YARP (Yet Another Reverse Proxy) `IHttpForwarder` |
| **协议逻辑** | 嵌入在 Service/Handler 层中 | 封装在 `IChatModelClient` 实现中 |

## 2. 路由与平台映射 (Route & Platform Mapping)

### Sub2API
- **Gemini:** `/v1beta/models`, `/v1beta/models/{model}:generateContent` 等。
- **OpenAI:** `/openai/v1/responses`, `/v1/chat/completions` (通过网关)。
- **Antigravity:** 隐式通过 `/antigravity` 路径或回退逻辑。
- **逻辑:** Handler 针对 API 结构特定实现。验证 `apiKey.Group.Platform`。

### AiRelay
- **Gemini Account:** `/gemini/{**catch-all}`
- **Gemini API:** `/gemini-api/{**catch-all}`
- **Claude Account:** `/claude/{**catch-all}`
- **Antigravity:** `/antigravity/{**catch-all}`
- **OpenAI:** `/openai/{**catch-all}`
- **逻辑:** `Program.cs` 将路径映射到 `ProviderPlatform` 元数据。中间件使用 `IChatModelClientFactory` 根据元数据实例化正确的客户端逻辑。

## 3. 下游请求处理 (Downstream Request Processing)

### URL 与 Header 处理
- **Sub2API:** Handler 手动解析 URL 参数 (例如 `parseGeminiModelAction`)。
- **AiRelay:** `DownstreamRequestProcessor` 捕获路径、查询参数和 Headers。`IChatModelClient.ExtractDownstreamInfo` 执行特定于平台的提取 (例如从 URL 或 Body 中解析 Model ID)。

### 会话哈希与粘性会话 (关键差异)
**Sub2API (`gemini_v1beta_handler.go`):**
1.  **Gemini CLI 支持:** 显式使用正则 `/\.gemini/tmp/([A-Fa-f0-9]{64})` 从请求体中的 `tmp` 目录路径提取会话哈希。
2.  **特权用户:** 将 `x-gemini-api-privileged-user-id` header 与 tmp 哈希组合。
3.  **回退:** 使用通用 Body 哈希或摘要链匹配。

**AiRelay (`GeminiAccountChatModelClient.cs`):**
1.  **标准 Headers:** 检查 `conversation_id`, `x-conversation-id` 等。
2.  **Body 内容:** 对 `contents` 中*第一条消息的文本*进行哈希。
3.  **缺失:** **未实现 Gemini CLI `tmp` 目录哈希提取。** 这是一个功能缺口，可能会导致 Gemini CLI 用户的粘性会话失效（上下文丢失）。

## 4. 上游转发逻辑 (Upstream Forwarding Logic)

### Gemini / Antigravity
**Sub2API:**
- **URL:** 将 `/v1beta` 转换为 `/v1internal`。
- **Body:** 将请求包装在 `{"request": ..., "project": ..., "model": ...}` 结构中。
- **Antigravity 回退:** Handler 中有显式逻辑，在 Gemini 账号失败时切换到 Antigravity 服务。

**AiRelay (`GeminiAccountChatModelClient.cs` & `AntigravityChatModelClient.cs`):**
- **URL:** 类似的转换逻辑 (`/v1internal:{operation}`)。
- **Body:** `TransformRequestAsync` 处理包装。
- **Project ID:** 从 `ConnectionOptions.ExtraProperties` 注入。
- **Antigravity 特性:**
    - **身份:** 注入 `SystemIdentity` 和 `SilentBoundaryPrompt`。
    - **请求类型:** 检测 `agent` vs `web_search` vs `image_gen`。
    - **工具:** 修复 Gemini CLI 工具的 `parametersJsonSchema`。
    - **签名:** 从缓存注入 `thoughtSignature`。
    - **降级:** 为 Antigravity 实现了 Level 1 (移除签名) 和 Level 2 (移除工具) 的降级策略。

### OpenAI / Codex
**Sub2API:**
- **Codex:** 检测 User-Agent，注入 `instructions`。

**AiRelay (`OpenAiChatModelClient.cs`):**
- **Codex 模式:** 强制使用 `chatgpt.com` 后端的逻辑，注入 `OpenAI-Beta` headers，并根据 User-Agent 设置 `originator`。
- **Instructions:** 如果缺失或为默认值，则注入 `CodexInstructions`。
- **模型规范化:** `gpt-4o` -> `gpt-4o-2024-05-13`。

## 5. 账号选择与可靠性 (Account Selection & Reliability)

### 调度 (Scheduling)
- **Sub2API:** `GatewayService` 中的 `SelectAccountWithLoadAwareness`。支持 "单账号重试 (Single Account Retry)" 标记，以便优雅地处理 503 错误而不立即切换。
- **AiRelay:** `smartProxyAppService.SelectAccountAsync`。中间件循环处理重试。
    - **重试逻辑:** 中间件显式循环。由 `AnalyzeErrorAsync` 决定 `FailureInstruction` (重试当前账号 vs 切换账号)。
    - **退避:** 当所有账号被排除时进行指数退避。

### 错误处理 (Error Handling)
- **Sub2API:** `ErrorPassthroughService` 集成在 Handler 中，将上游错误映射为自定义响应。
- **AiRelay:** `chatModelClient.AnalyzeErrorAsync` 对错误进行分类 (RateLimit, SignatureError)。中间件决定行动。
    - **缺口:** `ai-relay` 的 "错误透传 (Error Passthrough)"（自定义用户定义的错误映射）逻辑在中间件中不如 `sub2api` 的显式服务调用明显。

## 6. 响应与 I/O (Response & I/O)

- **Sub2API:** 手动流式读/写。异步 Goroutine 记录用量以避免阻塞。
- **AiRelay:**
    - **YARP:** 使用 `IHttpForwarder`，高度优化流式传输。
    - **Token 追踪:** `TokenTrackingStream` 包装响应流，实时计算 Token 而无需缓冲整个响应（除非需要日志记录）。
    - **签名捕获:** `SignatureTrackingStream` (Antigravity) 从 SSE 流中捕获 `thoughtSignature` 用于缓存。

## 7. 关键发现与建议 (Key Findings & Recommendations)

1.  **Gemini CLI 会话哈希 (高优先级):**
    - `ai-relay` 必须在 `GeminiAccountChatModelClient.ExtractDownstreamInfo` 中实现 `.gemini/tmp/` 哈希的正则提取。没有这个，Gemini CLI 的使用可能会因糟糕的粘性会话而受到影响（上下文丢失）。

2.  **Antigravity 降级策略:**
    - `ai-relay` 在 `AntigravityChatModelClient` 和 `SmartReverseProxyMiddleware` 中实现了完善的降级机制 (Level 1/2)。这相比 `sub2api` 分散的逻辑显得*更先进*或结构更清晰。

3.  **代码结构:**
    - `ai-relay`使用了更清晰的策略模式 (`IChatModelClient`) + 中间件，使得添加新提供商更容易，而无需复制 HTTP 处理逻辑（如 `sub2api` 的独立 Handlers 所示）。

4.  **GET /models:**
    - `sub2api` 显式处理 `GET /v1beta/models` 以返回回退列表。`ai-relay` 目前是盲目转发。如果上游（如 Code Assist）不支持标准的 List Models 端点或返回 403，`ai-relay` 可能需要为此路径添加回退拦截器。

5.  **错误透传:**
    - 验证 `AiRelay` 是否需要移植 `sub2api` 中可配置的 `ErrorPassthroughService` 逻辑，以允许管理员为特定的上游代码自定义错误消息。
