# 提供商账户分组管理 - 综合设计文档

**Sprint**: 2026-01 SP2  
**创建时间**: 2026-01-19  
**最后更新**: 2026-01-30  
**状态**: 开发中

---

## 1. 需求与概述

### 1.1 业务背景
当前系统的账户调度策略较为单一，无法满足复杂的业务场景需求。本功能旨在通过引入"账户分组"概念，实现更灵活的资源调度和管理。

主要解决以下问题：
1.  **差异化调度**：不同业务场景（如高优先级客户 vs 普通用户）需要不同的资源分配策略。
2.  **分组隔离**：将账户按用途、质量或成本分组，不同 ApiKey 绑定不同分组。
3.  **会话连续性**：在对话场景中，确保同一会话周期的请求由同一账户处理（粘性会话）。
4.  **路由管理**：建立 ApiKey 到账户分组的灵活路由映射。

### 1.2 核心功能

1.  **分组管理**
    *   **维度**：每个分组归属于特定平台（`Platform`），如 Gemini、OpenAI。
    *   **策略**：每个分组配置独立的调度策略（加权随机、最少请求、优先级）。
    *   **会话保持**：支持粘性会话（Sticky Session）配置，包含开关及过期时间。

2.  **账户关联 (多对多)**
    *   一个账户可归属多个分组。
    *   关联属性：
        *   `Priority`（优先级）：用于 Priority 策略。
        *   `Weight`（权重）：用于 WeightedRandom 策略。

3.  **ApiKey 路由 (绑定)**
    *   ApiKey 根据平台类型绑定到特定分组。
    *   **约束**：同一 ApiKey 在同一平台下只能绑定一个分组（确保路由确定性）。

4.  **调度策略详解**
    *   **WeightedRandom (加权随机)**：根据权重概率分配请求，适用于负载均衡。
    *   **LeastRequests (最少请求数)**：优先选择当前负载最小的账户。
    *   **Priority (优先级降级)**：严格按优先级顺序使用，高优先级不可用时自动降级。

### 1.3 开发原则
*   **规范遵循**：严格遵守 `docs/standards/code_standard/backend_develop.md`。
*   **防御性设计**：利用数据库唯一索引和应用层校验双重保障数据一致性。
*   **极简设计**：避免过度工程，聚焦核心调度能力的实现。

---

## 2. 后端架构与数据模型

后端遵循 DDD 分层架构，在 `AiRelay.Domain`, `AiRelay.Application`, `AiRelay.Infrastructure` 中实现。

### 2.1 实体模型 (Domain Layer)

#### 2.1.1 核心实体

*   **ProviderGroup (聚合根)**
    *   位置：`AiRelay.Domain/ProviderGroups/Entities/ProviderGroup.cs`
    *   属性：
        *   `Id`: Guid
        *   `Name`: string (分组名称)
        *   `Platform`: AccountPlatform (平台类型)
        *   `SchedulingStrategy`: GroupSchedulingStrategy (调度策略枚举)
        *   `EnableStickySession`: bool (是否启用粘性会话)
        *   `StickySessionExpirationDays`: int (过期天数)
    *   索引：`UX_Name_Platform` (Name + Platform 唯一)

*   **ProviderGroupAccountRelation (关联实体)**
    *   位置：`AiRelay.Domain/ProviderGroups/Entities/ProviderGroupAccountRelation.cs`
    *   属性：
        *   `ProviderGroupId`: Guid
        *   `AccountTokenId`: Guid
        *   `Weight`: int (默认 1)
        *   `Priority`: int (默认 0)
    *   索引：`UX_GroupId_AccountId` (唯一，防止重复关联)

*   **ApiKeyProviderGroupBinding (绑定实体)**
    *   位置：`AiRelay.Domain/ApiKeys/Entities/ApiKeyProviderGroupBinding.cs`
    *   属性：
        *   `ApiKeyId`: Guid
        *   `ProviderGroupId`: Guid
        *   `Platform`: AccountPlatform (冗余字段，用于约束)
    *   索引：`UX_ApiKeyId_Platform` (唯一，确保单平台单路由)

#### 2.1.2 值对象 (Value Objects)

*   **GroupSchedulingStrategy (枚举)**
    ```csharp
    public enum GroupSchedulingStrategy
    {
        WeightedRandom = 1, // 加权随机
        LeastRequests = 2,  // 最少请求数
        Priority = 3        // 优先级降级
    }
    ```

### 2.2 基础设施 (Infrastructure Layer)

在 `AiRelayDbContext` 中配置 Fluent API：
*   配置实体的软删除过滤器。
*   配置复合唯一索引。
*   配置级联删除行为（删除分组 -> 删除关联；删除账户 -> 删除关联）。

---

## 3. 应用层设计 (Application Layer)

### 3.1 DTO 定义

遵循 `*InputDto` / `*OutputDto` 命名规范，使用 DataAnnotations 进行校验。

#### 分组操作
*   **CreateProviderGroupInputDto**
    *   `Name` (Required, MaxLength)
    *   `Platform` (Required)
    *   `Strategy` (Required)
    *   `EnableStickySession`, `StickySessionExpirationDays`
*   **UpdateProviderGroupInputDto**: 仅包含可变字段 (Name, Strategy)。
*   **UpdateStickySessionInputDto**: 专门用于更新会话配置。

#### 账户关联操作
*   **AddGroupAccountInputDto**
    *   `AccountId` (Required)
    *   `Priority` (Min 0)
    *   `Weight` (1-100)
*   **UpdateGroupAccountInputDto**: 更新 Priority 和 Weight。

#### 绑定操作
*   **BindGroupInputDto**: `Platform`, `GroupId`.

### 3.2 AutoMapper 映射
在 `ProviderGroupProfile` 中配置：
*   `ProviderGroup` <-> `ProviderGroupDto`
*   `ProviderGroupAccountRelation` -> `GroupAccountRelationDto` (Flatten Account Name)
*   `ApiKeyProviderGroupBinding` -> `ApiKeyGroupBindingDto` (Flatten Group Name)

---

## 4. API 接口设计

### 4.1 分组管理 (`/api/v1/provider-groups`)
*   `GET /`: 获取分组列表（支持分页、按名称/平台过滤）。
*   `POST /`: 创建新分组。
*   `PUT /{id}`: 更新分组基本信息。
*   `DELETE /{id}`: 删除分组（级联删除关联）。
*   `PUT /{id}/sticky-session`: 更新粘性会话配置。
*   `GET /metrics`: 获取分组统计指标（总数、活跃绑定等）。

### 4.2 组内账户管理 (`/api/v1/provider-groups/{groupId}/accounts`)
*   `GET /`: 获取该分组下的账户关联列表。
*   `POST /`: 添加账户到分组。
*   `PUT /{accountId}`: 更新账户在分组内的配置（权重/优先级）。
*   `DELETE /{accountId}`: 从分组移除账户。

---

## 5. 前端设计方案 (frontend-gemini)

### 5.1 技术栈
*   **框架**: Angular v21 (Signals, Control Flow)
*   **UI库**: PrimeNG v21
*   **样式**: Tailwind CSS v4
*   **状态管理**: Angular Signals

### 5.2 页面布局与交互

#### 5.2.1 统计看板 (Metrics Cards)
页面顶部展示关键指标：
*   **总分组数**
*   **平台分布** (Gemini / Claude / OpenAI)
*   **活跃绑定数**
*   **粘性会话开启数**

#### 5.2.2 核心管理表格 (Group Table)
使用 `p-table` 组件，启用 **Row Expansion** (行展开) 功能。

*   **主行 (分组信息)**
    *   展示：名称、平台(Icon)、策略(Badge)、会话保持状态(Toggle)。
    *   操作：编辑、删除。
*   **展开行 (账户关联管理)**
    *   **添加栏**：内嵌表单，支持选择同平台未关联账户，设置初始权重/优先级。
    *   **关联列表**：展示已关联账户。支持**行内编辑** (In-place Edit) 权重和优先级，支持快速移除。

#### 5.2.3 弹窗交互
*   **Create/Edit Dialog**: 包含分组基础信息表单。
*   **Validation**: 实时表单验证，错误提示。

### 5.3 目录结构
```text
src/app/features/platform/
├── components/
│   └── provider-group/
│       ├── provider-group.component.ts       # 容器组件
│       ├── widgets/
│       │   ├── group-table/                  # 列表与行展开逻辑
│       │   ├── group-edit-dialog/            # 新增/编辑弹窗
│       │   └── group-metrics/                # 顶部指标卡片
├── models/
│   └── provider-group.dto.ts                 # TS 接口定义
└── services/
    └── provider-group.service.ts             # API 通信
```

---

## 6. 实施计划 (Roadmap)

预计总工期：5-6 天

### 阶段一：领域层开发 (Domain) [Backend]
1.  创建 `ProviderGroup`, `Relation`, `Binding` 实体。
2.  定义 `GroupSchedulingStrategy` 枚举。
3.  实现 `ProviderGroupDomainService` (核心调度逻辑预埋)。

### 阶段二：基础设施与配置 (Infrastructure) [Backend]
1.  配置 `AiRelayDbContext` (DbSet, OnModelCreating)。
2.  添加唯一索引和级联删除约束。
3.  生成并应用 EF Core Migration。

### 阶段三：应用层开发 (Application) [Backend]
1.  定义所有 Input/Output DTOs。
2.  配置 AutoMapper Profile。
3.  实现 `ProviderGroupAppService` (CRUD 逻辑)。

### 阶段四：API 暴露 (Api) [Backend]
1.  创建 `ProviderGroupController`。
2.  实现所有设计中的 RESTful 接口。
3.  Swagger 文档验证。

### 阶段五：前端开发 (Frontend)
1.  定义 Angular Models (DTOs)。
2.  实现 `ProviderGroupService`。
3.  开发指标卡片和主表格组件。
4.  实现行展开的账户管理逻辑。
5.  联调 API。

### 阶段六：测试与交付
1.  **单元测试**: 覆盖核心调度算法和 DTO 校验。
2.  **集成测试**: 验证分组-账户关联、ApiKey 绑定的完整流程。
3.  **UI 验收**: 验证交互流畅度和响应式布局。