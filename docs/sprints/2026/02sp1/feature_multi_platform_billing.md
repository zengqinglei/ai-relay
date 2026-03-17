# 多平台计费系统最佳实践方案

## 1. 需求描述

为了实现对 OpenAI、Claude、Gemini、Antigravity 等多平台模型请求的精确计费与成本核算，本项目需要构建一套可扩展的计费系统。

### 1.1 核心目标
*   **精确计费**：基于 Token 使用量（Input/Output）和模型单价计算基础费用 (`BaseCost`)。
*   **多平台支持**：兼容不同厂商的 Token 统计格式（如 Gemini 的 `usageMetadata`，Claude 的 `usage`）。
*   **定价策略**：支持加载 LiteLLM 标准格式的 `model_pricing.json`，并支持动态更新。
*   **成本审计**：记录原始费用、应用倍率后的最终费用 (`FinalCost`)，以及当时生效的倍率。
*   **架构解耦**：将用量记录 (`UsageRecords`) 从账号管理模块剥离，形成独立的限界上下文。

### 1.2 参考设计
*   **数据结构 (参考 sub2api)**：
    *   采用详尽的字段设计，明确区分 `InputTokens`, `OutputTokens`, `CacheReadTokens` (缓存读取), `CacheCreationTokens` (缓存写入)。
    *   引入 `TotalCost` (标准费用) 与 `ActualCost` (实付费用) 概念，对应本系统的 `BaseCost` 与 `FinalCost`。
*   **定价源 (参考 claude-relay-service)**：
    *   采用 **LiteLLM 格式的 `model_pricing.json`** 作为标准数据源。
    *   支持定时从远程 URL 更新价格表。

---

## 2. 核心策略

### 2.1 架构调整与模块化

采用 **DDD 分层架构**，新增顶层模块 `UsageRecords`，将计费与用量逻辑从 `ProviderAccounts` 中解耦。

#### 调整后的树形目录结构

```text
E:\workspace\ai\leistd-ai-relay\ai-relay\backend\src\
├── AiRelay.Domain\
│   ├── UsageRecords\                          # [新增] 独立的用量记录模块
│   │   ├── Entities\
│   │   │   └── UsageRecord.cs                 # [迁移&重构] 原 TokenUsageRecord，增加计费字段
│   │   ├── DomainServices\
│   │   │   └── UsageRecordDomainService.cs    # [核心] 负责价格计算、状态流转、倍率应用
│   │   ├── Providers\
│   │   │   └── IPricingProvider.cs            # [接口] 定义获取模型定价的能力
│   │   └── Events\
│   │       └── UsageRecordCreatedEvent.cs     # [新增] 领域事件
│   └── ProviderAccounts\                      # [清理] 移除旧的 Usage 相关实体逻辑
│
├── AiRelay.Infrastructure\
│   ├── UsageRecords\                          # [新增] 基础设施实现
│   │   ├── Providers\
│   │   │   └── LiteLlmPricingProvider.cs      # [实现] 读取 JSON 缓存并实现 IPricingProvider
│   │   ├── BackgroundJobs\
│   │   │   └── PricingUpdateHostedService.cs  # [任务] 定时从远程 URL 更新 model_pricing.json
│   │   └── Persistence\
│   │       └── UsageRecordConfiguration.cs    # [EF Core] 实体映射配置
│   └── Persistence\
│       └── AiRelayDbContext.cs                # [调整] DbSet<UsageRecord>
│
├── AiRelay.Application\
│   ├── UsageRecords\                          # [新增] 应用层模块
│   │   ├── AppServices\
│   │   │   └── UsageRecordAppService.cs       # [协调] 接收 Worker 请求，调用 DomainService
│   │   ├── Dtos\
│   │   │   ├── StartUsageRecordInputDto.cs
│   │   │   ├── FinishUsageRecordInputDto.cs   # [规范] 包含 Token 数、模型名、状态码等
│   │   │   └── UsageRecordOutputDto.cs
│   │   └── Mappings\
│   │       └── UsageRecordProfile.cs
│   └── ProviderAccounts\                      # [清理] 移除旧 Usage DTO
│
└── AiRelay.Api\
    ├── Middleware\
    │   └── SmartProxy\
    │       └── TokenExtraction\               # [新增] 协议解析层
    │           ├── ITokenExtractor.cs         # [接口] 定义从 Response 流提取 Token 的能力
    │           ├── OpenAiTokenExtractor.cs
    │           ├── GeminiTokenExtractor.cs
    │           └── ClaudeTokenExtractor.cs
    └── HostedServices\
        └── Workers\
            └── AccountUsageProcessingWorker.cs # [调整] 消费 Channel 数据，调用 UsageRecordAppService
```

### 2.2 核心组件设计

1.  **领域实体 (`UsageRecord`)**：
    *   **基础字段**：`Id`, `Duration`, `StatusCode`, `ErrorMessage`.
    *   **计费字段**：
        *   `Model` (string): 实际调用的模型名称。
        *   `InputTokens`, `OutputTokens` (int): 用量。
        *   `BaseCost` (decimal): 基于 `model_pricing.json` 计算的理论成本。
        *   `AppliedMultiplier` (decimal): 记录计算时的倍率 (Group * Account)。
        *   `FinalCost` (decimal): `BaseCost * AppliedMultiplier`，实际扣费金额。

2.  **定价服务 (`LiteLlmPricingProvider`)**：
    *   **职责**：解析 LiteLLM 格式的 JSON，提供 `GetPricingAsync(model)` 方法。
    *   **缓存**：使用 `IMemoryCache` 缓存解析后的价格字典。
    *   **更新**：配合 `PricingUpdateHostedService` 每 24 小时更新一次。

3.  **Token 提取 (`ITokenExtractor`)**：
    *   **位置**：保留在 API 层，因为涉及具体的 HTTP 协议解析（尤其是 SSE 流处理）。
    *   **职责**：从 `HttpContext` 或 `MemoryStream` 中提取不同厂商的 Token 使用量，封装为 DTO 传递给后台任务。

4.  **前端适配**：
    *   更新 Dashboard 的流量日志表格，增加 `Tokens (In/Out)` 和 `Cost` 列。
    *   利用 Mock 数据先行调试 UI 展示。

---

## 3. 实施计划

### 阶段一：基础设施准备 (Infrastructure)

*   [ ] **1.1 定义接口**：在 `AiRelay.Domain` 创建 `IPricingProvider` 接口及 `ModelPricingInfo` 值对象。
*   [ ] **1.2 实现 Provider**：在 `AiRelay.Infrastructure` 实现 `LiteLlmPricingProvider`，支持解析 LiteLLM JSON。
*   [ ] **1.3 实现后台任务**：创建 `PricingUpdateHostedService`，实现定时下载逻辑，并注入到 DI 容器。
*   [ ] **1.4 资源文件**：引入初始版本的 `model_pricing.json` 到项目资源中作为 Fallback。

### 阶段二：领域层重构 (Domain)

*   [ ] **2.1 创建模块**：建立 `AiRelay.Domain.UsageRecords` 目录。
*   [ ] **2.2 迁移实体**：将 `TokenUsageRecord` 迁移为 `UsageRecord`，并添加 Cost 相关字段。
*   [ ] **2.3 领域服务**：实现 `UsageRecordDomainService`，编写 `ProcessCompletionAsync` 方法，串联价格计算与状态更新逻辑。
*   [ ] **2.4 清理旧代码**：移除 `ProviderAccounts` 中过时的 Usage 逻辑。

### 阶段三：持久化与数据迁移 (Persistence)

*   [ ] **3.1 配置映射**：编写 `UsageRecordConfiguration`，配置字段精度（如 Cost 建议 `decimal(18,8)`）。
*   [ ] **3.2 更新 DbContext**：在 `AiRelayDbContext` 中注册 `DbSet<UsageRecord>`。
*   [ ] **3.3 数据库迁移**：生成并执行 EF Core Migration (`AddUsageRecordsModule`)。

### 阶段四：应用层与 API 层 (Application & API)

*   [ ] **4.1 DTO 定义**：创建 `FinishUsageRecordInputDto`，包含 `InputTokens`, `OutputTokens`, `Model` 等字段，并添加 DataAnnotations。
*   [ ] **4.2 应用服务**：实现 `UsageRecordAppService`，协调 `UsageRecordDomainService` 完成业务。
*   [ ] **4.3 Token 提取器**：在 API 层实现 `OpenAiTokenExtractor` 等，负责解析响应流。
*   [ ] **4.4 中间件集成**：更新 `SmartReverseProxyMiddleware`，在请求结束时调用 Extractor 并发送消息到 Channel。
*   [ ] **4.5 消费端更新**：更新 `AccountUsageProcessingWorker` 以适配新的 DTO 和 AppService。

### 阶段五：前端适配 (Frontend)

*   [ ] **5.1 更新 Mock**：在 `_mock` 中为 API 响应添加 `cost` 和 `tokens` 字段。
*   [ ] **5.2 Dashboard 调整**：修改 `Traffic` 页面，在 PrimeNG 表格中展示 Token 消耗和 Cost 金额。
*   [ ] **5.3 格式化**：使用 Angular Pipe (`currency`, `number`) 优化数值展示。

### 阶段六：验证与测试

*   [ ] **6.1 单元测试**：测试 `UsageRecordDomainService` 的计费逻辑（包括倍率计算）。
*   [ ] **6.2 集成测试**：模拟完整 HTTP 请求，验证 Token 是否被正确提取并记录到数据库。
*   [ ] **6.3 价格更新测试**：验证后台任务是否能成功更新价格缓存。
