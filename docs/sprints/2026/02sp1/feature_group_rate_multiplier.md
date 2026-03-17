# 分组倍率配置（Rate Multiplier）特性设计方案

## 1. 需求描述

为了支持差异化的商业计费策略，需要在 **渠道分组（Provider Group）** 维度增加 **倍率（Rate Multiplier）** 配置。

*   **功能定义**：管理员可以为不同的渠道分组设置费率倍数。
*   **默认值**：`1.0`（即原价）。
*   **业务影响**：
    *   该倍率将参与 `UsageRecord` 的最终费用计算。
    *   计算公式：`FinalCost = BaseCost * GroupMultiplier * (AccountMultiplier || 1.0)`。
*   **涉及范围**：
    *   后端：数据库字段变更、实体更新、DTO 更新、CRUD 逻辑适配。
    *   前端：分组列表展示、详情页展示、新增/编辑弹窗表单。
    *   Mock：前端 Mock 数据结构同步更新。

---

## 2. 核心策略

### 2.1 架构调整与目录结构

本次调整遵循 DDD 分层架构，主要涉及 `AiRelay.Domain` (实体), `AiRelay.Infrastructure` (持久化), `AiRelay.Application` (DTO/映射), 以及前端 `frontend-gemini` 的页面组件。

#### 涉及文件结构树

```text
E:\workspace\ai\leistd-ai-relay\ai-relay\
├── backend\src\
│   ├── AiRelay.Domain\
│   │   └── ProviderGroups\
│   │       └── Entities\
│   │           └── ProviderGroup.cs             # [修改] 新增 RateMultiplier 属性
│   ├── AiRelay.Infrastructure\
│   │   └── Persistence\
│   │       └── EntityConfigurations\
│   │           └── ProviderGroupConfiguration.cs # [修改] 配置精度 (Decimal 18,4)
│   ├── AiRelay.Application\
│   │   └── ProviderGroups\
│   │       ├── Dtos\
│   │       │   ├── ProviderGroupOutputDto.cs    # [修改] 增加字段
│   │       │   ├── CreateProviderGroupInput.cs  # [修改] 增加字段 & 校验
│   │       │   └── UpdateProviderGroupInput.cs  # [修改] 增加字段 & 校验
│   │       └── Mappings\
│   │           └── ProviderGroupProfile.cs      # [修改] 映射配置
│   └── AiRelay.Api\
│       └── ... (API 契约自动随 DTO 更新)
│
└── frontend-gemini\
    ├── _mock\
    │   └── provider-groups.ts                   # [修改] Mock 数据增加 rateMultiplier
    └── src\app\
        └── routes\
            └── provider-groups\
                ├── list\
                │   └── list.component.html      # [修改] 表格增加倍率列
                ├── detail\
                │   └── detail.component.html    # [修改] 详情页增加倍率展示
                └── components\
                    └── group-form\              # [修改] 表单增加数字输入框
                        ├── group-form.component.ts
                        └── group-form.component.html
```

### 2.2 后端设计 (.NET 10)

1.  **实体 (Domain)**:
    *   在 `ProviderGroup` 中添加 `public decimal RateMultiplier { get; private set; } = 1.0m;`。
    *   更新构造函数和 `Update` 方法以接收此参数。

2.  **持久化 (Infrastructure)**:
    *   EF Core 配置：`builder.Property(x => x.RateMultiplier).HasPrecision(10, 4).HasDefaultValue(1.0m);`。
    *   生成 Migration：`AddRateMultiplierToProviderGroup`。

3.  **交互 (Application)**:
    *   InputDto 添加 `[Range(0.01, 100)]` 校验，防止恶意的 0 或负数倍率。

### 2.3 前端设计 (Angular v21 + PrimeNG v21)

1.  **列表页**: 使用 `p-table` 展示倍率，建议使用 `<p-tag>` 或特殊颜色标记非 1.0 的倍率。
2.  **表单页**: 使用 `p-inputNumber` 组件，设置 `mode="decimal"`，`minFractionDigits="2"`，`step="0.1"`。
3.  **样式**: 使用 Tailwind CSS v4 (`text-gray-500`, `font-mono`) 优化数字展示。

---

## 3. 实施计划

### 阶段一：后端核心变更

*   [ ] **Step 1.1**: 修改 `ProviderGroup` 实体，添加 `RateMultiplier` 属性及更新逻辑。
*   [ ] **Step 1.2**: 修改 `ProviderGroupConfiguration`，配置数据库字段精度。
*   [ ] **Step 1.3**: 创建并执行 EF Core Migration。
*   [ ] **Step 1.4**: 更新 `ProviderGroupDto`、`CreateProviderGroupInput`、`UpdateProviderGroupInput`。
*   [ ] **Step 1.5**: 更新 `ProviderGroupProfile` 映射配置。

### 阶段二：前端 Mock 与模型适配

*   [ ] **Step 2.1**: 更新前端 TypeScript 接口 `ProviderGroup` (src/app/core/models/provider-group.ts)。
*   [ ] **Step 2.2**: 更新 `_mock/provider-groups.ts`，为模拟数据添加默认 `rateMultiplier: 1`。

### 阶段三：前端页面调整

*   [ ] **Step 3.1**: 修改 **列表页 (List)**，在表格中增加“费率倍数”列。
*   [ ] **Step 3.2**: 修改 **表单组件 (Dialog)**，新增 `Rate Multiplier` 输入项（默认为 1）。
*   [ ] **Step 3.3**: 修改 **详情页 (Detail)**，在概览卡片中展示当前倍率。

### 阶段四：验证与联调

*   [ ] **Step 4.1**: 启动后端，验证数据库字段已创建。
*   [ ] **Step 4.2**: 启动前端 (使用 Mock)，验证 CRUD 流程中倍率字段的存取是否正常。
*   [ ] **Step 4.3**: 关闭 Mock，前后端联调，创建一个倍率为 `1.5` 的分组并验证保存成功。
