# 模型测试功能实现方案文档 (Model Test)

## 1. 需求描述

### 1.1 功能目标
为“渠道账号管理”列表增加“模型测试”功能。用户点击按钮后，系统应能实时验证该账号配置（API Key 或 OAuth Token、代理地址等）是否有效，并以流式（打字机效果）展示 AI 模型的响应内容。

### 1.2 核心价值
*   **可用性验证**：不仅验证网络连通性，还验证模型本身是否可用（Quota, Permission）。
*   **即时反馈**：通过 SSE (Server-Sent Events) 实时展示交互过程。
*   **多平台支持**：支持 OpenAI, Claude, Gemini 以及 **Antigravity** (Google 内部代理协议) 等主流平台的多种接入模式。

---

## 2. 最终实现方案

### 2.1 后端架构设计 (DDD + Abstract Factory)

*   **Domain Layer (核心层)**
    *   **位置**：`AiRelay.Domain/Shared/ExternalServices/ChatModel/`
    *   **抽象接口**：`IChatModelClient`
        *   `StreamChatAsync`: 核心流式对话方法。
        *   `Configure`: 注入配置（Credential, BaseUrl, ProjectId）。
        *   `ValidateConnectionAsync`: 验证连接并返回元数据（如 Project ID）。
    *   **抽象工厂**：`IChatModelClientFactory`
        *   负责将 `AccountToken` 实体转换为 `ChatModelConnectionOptions` 并创建对应的 Client。
    *   **数据模型**：
        *   `Dto/ChatStreamEvent.cs`: 统一流式响应事件结构。
        *   `Dto/ChatModelConnectionOptions.cs`: 统一连接配置。
        *   `Dto/ConnectionValidationResult.cs`: 连接验证结果。
    *   **领域服务**：`AccountTokenDomainService`
        *   `PrepareAccountAsync`: 负责 Token 刷新（针对 Account 类型）和 Project ID 自动获取/回填（针对 Gemini/Antigravity）。

*   **Infrastructure Layer (基础设施层)**
    *   **位置**：`AiRelay.Infrastructure/Shared/ExternalServices/ChatModel/`
    *   **策略实现**：
        *   `GeminiAccountChatModelClient.cs`: 
            *   处理 `GEMINI_ACCOUNT` (Code Assist)。
            *   使用 `cloudcode-pa.googleapis.com/v1internal` 接口。
            *   集成原 `LoadCodeAssistAsync` 逻辑用于 Project ID 获取。
        *   `AntigravityChatModelClient.cs`:
            *   处理 `ANTIGRAVITY` 协议（支持 Gemini 和 Claude 模型）。
            *   封装 Antigravity Envelope (`project`, `model`, `userAgent`, `request`)。
            *   支持 Claude 模型名称到 Antigravity 内部名称的自动映射 (`ModelMappingService`)。
        *   `GeminiApiChatModelClient.cs`: 处理 `GEMINI_APIKEY` (AI Studio)。
        *   `ClaudeChatModelClient.cs`: 处理 Claude 官方 API。
        *   `OpenAiChatModelClient.cs`: 处理 OpenAI 官方 API。
    *   **依赖注入**：使用 Keyed Services (`AddKeyedTransient`) 注册，Key 为 `ProviderPlatform` 枚举。

*   **Application Layer (应用层)**
    *   **服务方法**：`ProviderAccountAppService.DebugModelAsync`。
    *   **逻辑**：通过 `IChatModelClientFactory` 创建 Client，直接调用 `StreamChatAsync`，结果通过 `IAsyncEnumerable` 返回。
    *   **ProviderInitializer**：重构为使用 `AccountTokenDomainService` 进行账号预热。

*   **API Layer (接口层)**
    *   **端点**：`POST {id}/model-test`。
    *   **响应**：`text/event-stream` (SSE)。

### 2.2 前端架构设计 (Angular + PrimeNG)

*   **组件设计**：
    *   **组件名**：`ModelTestDialog`。
    *   **路径**：`.../components/model-test-dialog/`。
    *   **功能**：
        *   支持根据平台 (`GEMINI`, `CLAUDE`, `OPENAI`, `ANTIGRAVITY`) 动态加载模型列表。
        *   支持自定义 System Prompt (多行输入)。
        *   终端风格的流式输出展示。
    *   **图标组件**：`PlatformIcon` (共享组件)，支持各平台（含 Antigravity）的图标展示。

*   **枚举与配置**：
    *   `ProviderPlatform`: 新增 `ANTIGRAVITY`。

### 2.3 最终目录结构

```text
E:\workspace\ai\leistd-ai-relay\ai-relay\
├── backend\src\
│   ├── AiRelay.Domain\
│   │   ├── Shared\
│   │   │   └── ExternalServices\
│   │   │       └── ChatModel\
│   │   │           ├── IChatModelClient.cs
│   │   │           ├── IChatModelClientFactory.cs
│   │   │           └── Dto\
│   │   │               ├── ChatStreamEvent.cs
│   │   │               ├── ChatModelConnectionOptions.cs
│   │   │               └── ConnectionValidationResult.cs
│   │   └── ProviderAccounts\
│   │       ├── DomainServices\
│   │       │   └── AccountTokenDomainService.cs (Updated: Add PrepareAccountAsync)
│   │       └── Services\
│   │           └── IModelMappingService.cs (New: For Antigravity)
│   ├── AiRelay.Infrastructure\
│   │   ├── Shared\
│   │   │   └── ExternalServices\
│   │   │       └── ChatModel\
│   │   │           ├── ChatModelClientFactory.cs
│   │   │           ├── GeminiAccountChatModelClient.cs
│   │   │           ├── AntigravityChatModelClient.cs (New)
│   │   │           ├── GeminiApiChatModelClient.cs
│   │   │           ├── ClaudeChatModelClient.cs
│   │   │           └── OpenAiChatModelClient.cs
│   │   └── ProviderAccounts\
│   │       └── Services\
│   │           └── ModelMappingService.cs (New: Antigravity Model Mapping)
│   ├── AiRelay.Application\
│   │   └── ProviderAccounts\
│   │       ├── AppServices\
│   │       │   └── ProviderAccountAppService.cs (Updated: Use Factory & DomainService)
│   │       └── Dtos\
│   │           └── ChatMessageInputDto.cs
│   └── AiRelay.Api\
│       └── Controllers\
│           └── ProviderAccountController.cs (Updated: Add SSE Endpoint)
└── frontend-gemini\
    ├── src\app\
        ├── shared\
        │   ├── components\
        │   │   └── platform-icon\ (New)
        │   └── models\
        │       └── provider-platform.enum.ts (Updated: Add ANTIGRAVITY)
        └── features\platform\components\provider-account\
            └── components\
                └── model-test-dialog\
                    ├── model-test-dialog.ts (Updated: Layout & Models)
                    └── model-test-dialog.html
```

---

## 3. 关键变更点总结

1.  **Antigravity 深度集成**：
    *   不只是简单的代理，而是实现了完整的协议适配（Envelope 封装、Header 注入）。
    *   实现了 `ModelMappingService`，支持将 `gemini-3-flash-preview` 等通用名称映射为 Antigravity 内部名称。
2.  **Gemini Account 修正**：
    *   从错误的 Vertex AI 接口修正为正确的 `v1internal` Code Assist 接口。
3.  **领域逻辑下沉**：
    *   Token 刷新和 Project ID 获取逻辑统一收敛到 `AccountTokenDomainService`，消除了 AppService 和 Initializer 之间的代码重复。
4.  **前端体验优化**：
    *   重构了测试对话框布局，增加了自定义 System Prompt 输入。
    *   统一了平台图标展示逻辑。
