# 前端重构方案：从 Traffic 到 UsageQuery 的迁移

## 1. 背景目标

### 1.1 背景
后端架构已完成从传统的 `Traffic` 模块向领域驱动的 `UsageRecords` 模块的迁移。
主要变更包括：
*   **API 路由变更**: 原 `/api/v1/traffic/*` 接口已废弃，全面迁移至 `/api/v1/usage/query/*`。
*   **实体模型统一**: 废弃 `TokenUsageRecord`，统一使用 `AiRelay.Domain.UsageRecords.Entities.UsageRecord`。
*   **服务职责分离**: 引入了 `UsageLifecycleAppService` (写入) 和 `UsageQueryAppService` (查询)。

### 1.2 目标
前端需要与后端架构变更保持一致，目标如下：
1.  **全面去 Traffic 化**: 将前端代码中所有 `Traffic` 相关的命名（文件名、类名、变量名）迁移至 `Usage` 语义。
2.  **规范化命名**: 遵循 Angular 风格指南，使用 `kebab-case` 文件名和明确的类名（如 `UsageQueryService`, `UsageTrendChart`）。
3.  **零视觉回归**: 在重构逻辑的同时，严格保持现有的 UI 布局、样式和交互体验不变。

## 2. 核心策略

### 2.1 调整内容的树形目录结构

我们将对 `frontend-gemini` 项目进行如下结构调整：

```text
frontend-gemini/src/app/features/platform/
├── models/
│   ├── traffic.dto.ts              --> [RENAME] usage.dto.ts
│   └── dashboard.dto.ts            --> [UPDATE] 引用更新
│
├── services/
│   ├── traffic.service.ts          --> [RENAME] usage-query.service.ts
│   └── dashboard.service.ts        --> [UPDATE] 引用更新
│
├── components/dashboard/
│   ├── dashboard.ts                --> [UPDATE] 逻辑更新
│   ├── dashboard.html              --> [UPDATE] 绑定更新
│   │
│   └── widgets/
│       ├── metrics-cards/          --> [UPDATE] Input 从 traffic 改为 usage
│       ├── request-log-table/      --> [UPDATE] DTO 类型更新
│       │
│       └── traffic-chart/          --> [RENAME] usage-trend-chart/
│           ├── traffic-chart.ts    --> usage-trend-chart.ts
│           └── traffic-chart.html  --> usage-trend-chart.html
│
└── _mock/
    ├── api/
    │   └── traffic.ts              --> [RENAME] usage-query.ts
    └── index.ts                    --> [UPDATE] 导出更新
```

## 3. 实施计划

### 阶段一：基础架构迁移 (DTO & Service)

1.  **DTO 重构**:
    *   重命名 `traffic.dto.ts` 为 `usage.dto.ts`。
    *   重命名接口：`TrafficMetricsOutputDto` -> `UsageMetricsOutputDto` 等。
2.  **Service 重构**:
    *   重命名 `traffic.service.ts` 为 `usage-query.service.ts`。
    *   类名变更为 `UsageQueryService`，API 路径更新为 `/api/v1/usage/query`。
3.  **Mock 数据更新**:
    *   重命名 `_mock/api/traffic.ts` 为 `_mock/api/usage-query.ts`。
    *   更新内部路由定义。

### 阶段二：组件重命名与适配

4.  **Dashboard 组件**:
    *   更新 `dashboard.service.ts` 以注入 `UsageQueryService`。
    *   更新 `dashboard.html` 模板，将 `[traffic]` 属性绑定改为 `[usage]`。
5.  **MetricsCards 组件**:
    *   将 `@Input() traffic` 重命名为 `@Input() usage`。
    *   更新 HTML 模板中的属性访问。
6.  **Chart 组件重命名**:
    *   将 `widgets/traffic-chart` 目录重命名为 `widgets/usage-trend-chart`。
    *   文件重命名：`traffic-chart.ts` -> `usage-trend-chart.ts`。
    *   类名更新：`TrafficChart` -> `UsageTrendChart`。
    *   Selector 更新：`app-traffic-chart` -> `app-usage-trend-chart`。

### 阶段三：清理与验证

7.  **全引用检查**: 搜索项目中的 `traffic` 关键字，确保没有遗漏的业务逻辑引用。
8.  **编译验证**: 执行 `npm run build` 确保无类型错误。
9.  **运行时验证**: 启动应用，检查 Dashboard 各个板块的数据加载和图表渲染是否正常。
