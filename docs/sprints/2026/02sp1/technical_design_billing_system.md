# 计费系统重构与商业化能力技术方案

## 1. 概述 (Overview)

本方案旨在重构 AiRelay 的计费底层，使其具备**精确计费**、**多平台支持**及**商业化定价**能力。通过引入独立的 `UsageRecords` 模块和动态定价引擎，实现对 Token 用量的精准追踪，并结合分组倍率（Rate Multiplier）支持差异化的商业运营策略。

### 1.1 核心目标
*   **架构解耦**：将用量记录从 `ProviderAccounts` 剥离，建立独立的 `UsageRecords` 限界上下文。
*   **精确计费**：基于 LiteLLM 标准价格表 (`model_pricing.json`) 计算基础成本 (`BaseCost`)。
*   **商业化支持**：引入 **分组倍率 (Group Rate Multiplier)**，实现 `FinalCost = BaseCost * Multiplier` 的定价逻辑。
*   **多平台兼容**：统一处理 OpenAI, Claude, Gemini, Antigravity 的 Token 统计差异。

---

## 2. 核心架构设计 (Architecture)

### 2.1 模块化结构
遵循 DDD 分层架构，在后端引入全新的顶层模块 `UsageRecords`。

```text
E:\workspace\ai\leistd-ai-relay\ai-relay\backend\src\
├── AiRelay.Domain\
│   ├── UsageRecords\                          # [新增] 独立模块
│   │   ├── Entities\
│   │   │   └── UsageRecord.cs                 # [重构] 聚合 Token、Cost、Multiplier 信息
│   │   ├── DomainServices\
│   │   │   └── UsageRecordDomainService.cs    # [核心] 计费计算与状态流转
│   │   ├── Providers\
│   │   │   └── IPricingProvider.cs            # [接口] 价格获取抽象
│   │   └── Events\
│   │       └── UsageRecordCreatedEvent.cs
│   └── ProviderGroups\                        # [原有]
│       └── Entities\
│           └── ProviderGroup.cs               # [修改] 增加 RateMultiplier
│
├── AiRelay.Infrastructure\
│   ├── UsageRecords\
│   │   ├── Providers\
│   │   │   └── LiteLlmPricingProvider.cs      # [实现] 解析 model_pricing.json
│   │   └── BackgroundJobs\
│   │       └── PricingUpdateHostedService.cs  # [任务] 定时更新价格源
│   └── Persistence\
│       └── EntityConfigurations\
│           └── ProviderGroupConfiguration.cs  # [修改] 配置倍率精度
│
├── AiRelay.Application\
│   ├── UsageRecords\
│   │   ├── Dtos\
│   │   │   └── FinishUsageRecordInputDto.cs   # [规范] 包含 Token 与 Model
│   │   └── AppServices\
│   │   │   └── UsageRecordAppService.cs       # [协调] 串联计费与持久化
│   └── ProviderGroups\
│       └── Dtos\
│           └── ProviderGroupDto.cs            # [修改] 暴露倍率字段
│
└── AiRelay.Api\
    ├── Middleware\
    │   └── SmartProxy\
    │       └── TokenExtraction\               # [新增] 协议解析策略工厂
    └── HostedServices\
        └── Workers\
            └── AccountUsageProcessingWorker.cs
```

---

## 3. 详细设计 (Detailed Design)

### 3.1 定价引擎 (Pricing Engine)
*   **数据源**：采用 LiteLLM 标准格式的 `model_pricing.json`。
*   **更新机制**：`PricingUpdateHostedService` 启动时加载本地资源，随后每 24 小时从远程 URL 更新并缓存至 `IMemoryCache`。
*   **接口定义**：
    ```csharp
    public interface IPricingProvider {
        Task<ModelPricingInfo?> GetPricingAsync(string modelName, CancellationToken ct);
    }
    ```

### 3.2 计费与倍率逻辑 (Billing & Multiplier)
*   **计算公式**：
    $$
    \text{BaseCost} = (\text{InputTokens} \times P_{in}) + (\text{OutputTokens} \times P_{out})
    $$
    $$
    \text{FinalCost} = \text{BaseCost} \times \text{Group.RateMultiplier}
    $$
*   **实体设计 (`UsageRecord`)**：
    *   `BaseCost` (decimal): 基础API成本。
    *   `AppliedMultiplier` (decimal): 记录时刻的倍率快照。
    *   `FinalCost` (decimal): 实际扣费金额。
*   **分组扩展 (`ProviderGroup`)**：
    *   新增字段 `RateMultiplier` (decimal, default 1.0)。
    *   用于区分普通用户与 VIP 用户的计费策略。

### 3.3 Token 提取 (Token Extraction)
*   在 API 层实现 `ITokenExtractor` 策略模式：
    *   **OpenAI**: 读取 `usage` 字段（流式需开启 `stream_options`）。
    *   **Gemini**: 解析 `usageMetadata`。
    *   **Claude**: 解析 `usage` 事件。
*   通过 `Channel` 异步传递至后台 Worker，不阻塞主请求线程。

---

## 4. 前端适配策略 (Frontend Strategy)

### 4.1 仪表盘 (Dashboard)
*   **实时日志**：修改 `Traffic` 页面表格，新增列：
    *   **Tokens**: 展示 `Input / Output`。
    *   **Cost**: 展示计算后的 `FinalCost` (USD)，使用等宽字体对齐。
*   **数据源**：直接适配后端 API 返回的新 DTO 结构，无需新建 Model 文件。

### 4.2 分组管理 (Provider Groups)
*   **列表页**：增加“费率倍数 (Rate)”列，非 1.0 的倍率高亮显示。
*   **编辑/新增**：增加 `Rate Multiplier` 数字输入框：
    *   精度：2位小数。
    *   范围：0.01 - 100.00。
    *   步长：0.1。

---

## 5. 实施计划 (Implementation Plan)

### 阶段一：基础设施与领域建模 (Backend Foundation)
*   [ ] **1.1** 创建 `AiRelay.Domain.UsageRecords` 模块，迁移并改造 `UsageRecord` 实体。
*   [ ] **1.2** 修改 `ProviderGroup` 实体及 EF Core 配置，添加 `RateMultiplier`。
*   [ ] **1.3** 实现 `LiteLlmPricingProvider` 及后台更新任务 `PricingUpdateHostedService`。
*   [ ] **1.4** 执行数据库迁移 (Migration)。

### 阶段二：应用层与 API 适配 (Backend Logic)
*   [ ] **2.1** 实现 `UsageRecordDomainService`，封装 `BaseCost` 计算与倍率应用逻辑。
*   [ ] **2.2** 实现 `UsageRecordAppService`，协调领域服务与仓储。
*   [ ] **2.3** 开发 `TokenExtractor` 工厂及具体策略 (OpenAI/Gemini/Claude)。
*   [ ] **2.4** 改造 `SmartReverseProxyMiddleware`，集成 Token 提取与异步投递。

### 阶段三：前端开发 (Frontend)
*   [ ] **3.1** 更新 `_mock` 数据，为分组添加倍率，为日志添加 Cost/Token 字段。
*   [ ] **3.2** 改造 **分组管理** 页面 (List/Dialog)，支持倍率配置。
*   [ ] **3.3** 改造 **Dashboard** 流量日志表格，展示计费详情。

### 阶段四：联调与验收 (QA)
*   [ ] **4.1** 验证不同模型的 Token 提取准确性（尤其是流式响应）。
*   [ ] **4.2** 验证修改分组倍率后，新产生的请求费用是否正确计算 (`FinalCost` 变化)。
*   [ ] **4.3** 验证价格表自动更新机制。
