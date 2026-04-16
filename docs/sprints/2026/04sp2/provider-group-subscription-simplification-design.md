# 渠道分组与订阅管理简化设计方案

## 1、背景&目标

### 1.1 背景

当前系统围绕渠道账户、渠道分组、订阅绑定形成了一套较完整但偏重的配置体系：

1. `ProviderGroup` 负责定义分组元数据、调度策略、Sticky Session、倍率等
2. `ProviderGroupAccountRelation` 负责维护分组与账户的关系，同时承载 `priority`、`weight`
3. `ApiKeyProviderGroupBinding` 负责订阅与分组的绑定，并通过 `priority` 表达主分组与 fallback 分组

从当前代码结构看，复杂度主要来自 3 个点：

1. 分组创建和编辑时直接管理账户绑定，导致“分组配置”和“账户归属配置”耦合
2. 存在多种 `GroupSchedulingStrategy`，前端和后端都有大量条件分支
3. 权重和优先级当前挂在 `ProviderGroupAccountRelation` 上，导致同一账户在不同分组中语义不稳定，也使 UI 理解成本变高

相关现状可见：

1. 前端分组 DTO：
   [provider-group.dto.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/models/provider-group.dto.ts)
2. 分组编辑弹窗：
   [group-edit-dialog.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/components/provider-group/widgets/group-edit-dialog/group-edit-dialog.ts)
3. 后端分组实体：
   [ProviderGroup.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Domain/ProviderGroups/Entities/ProviderGroup.cs)
4. 分组账户关系实体：
   [ProviderGroupAccountRelation.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Domain/ProviderGroups/Entities/ProviderGroupAccountRelation.cs)
5. 订阅绑定实体：
   [ApiKeyProviderGroupBinding.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Domain/ApiKeys/Entities/ApiKeyProviderGroupBinding.cs)

### 1.2 目标

本次目标不是继续增强分组能力，而是把分组体系收敛成更符合日常运营习惯的轻配置模型。

核心目标：

1. 降低首次配置门槛
2. 让“账户归组”操作回到账户上下文，而不是分组上下文
3. 去掉对大多数用户无感知的调度策略复杂度
4. 保留订阅绑定中的 fallback 能力，但让交互更清晰
5. 前后端模型从“策略驱动”转向“默认分组 + 账户属性 + 绑定顺序”驱动

### 1.3 方案合理性评估

#### 结论

该方案整体合理，且更贴近真实运营实践。

#### 合理的部分

1. `default` 默认分组非常有必要
2. 分组编辑页移除“绑定账户入口”是正确方向
3. 删除多调度策略，统一单策略，能显著降低前后端复杂度
4. 订阅绑定保留不变，但优化交互，符合业务本质

#### 需要明确的收敛点

1. “权重、优先级迁移到账户”建议同时迁移，而不是只迁一个
2. 单策略建议定义为“优先级优先，权重作为同优先级内分配因子”
3. 订阅绑定 UI 不建议退化为纯多选，因 fallback 是有顺序的链路，不是集合

---

## 2、核心策略

### 2.1 核心设计原则

1. 默认可用
2. 账户归属前置
3. 分组配置轻量化
4. 调度模型单一化
5. fallback 显式可视

### 2.2 调整内容的树形目录结构

```text
frontend/
├─ src/app/features/platform/
│  ├─ components/
│  │  ├─ account-token/
│  │  │  ├─ account-token.ts
│  │  │  ├─ account-token.html
│  │  │  └─ widgets/
│  │  │     └─ account-edit-dialog/
│  │  │        ├─ account-edit-dialog.ts
│  │  │        └─ account-edit-dialog.html
│  │  ├─ provider-group/
│  │  │  ├─ provider-group.ts
│  │  │  ├─ provider-group.html
│  │  │  └─ widgets/
│  │  │     ├─ group-edit-dialog/
│  │  │     │  ├─ group-edit-dialog.ts
│  │  │     │  └─ group-edit-dialog.html
│  │  │     └─ group-table/
│  │  │        ├─ group-table.ts
│  │  │        └─ group-table.html
│  │  ├─ subscriptions/
│  │  │  └─ widgets/
│  │  │     └─ subscription-edit-dialog/
│  │  │        ├─ subscription-edit-dialog.ts
│  │  │        └─ subscription-edit-dialog.html
│  ├─ models/
│  │  ├─ account-token.dto.ts
│  │  ├─ provider-group.dto.ts
│  │  └─ subscription.dto.ts
│  └─ services/
│     ├─ account-token-service.ts
│     └─ provider-group-service.ts
├─ _mock/
│  ├─ api/
│  │  ├─ account-token.ts
│  │  ├─ provider-group.ts
│  │  └─ api-key.ts
│  └─ data/
│     ├─ account-token.ts
│     ├─ provider-group.ts
│     └─ api-key.ts

backend/
├─ src/AiRelay.Domain/
│  ├─ ProviderGroups/
│  │  ├─ Entities/
│  │  │  ├─ ProviderGroup.cs
│  │  │  └─ ProviderGroupAccountRelation.cs
│  │  ├─ DomainServices/
│  │  │  └─ ProviderGroupDomainService.cs
│  │  └─ ValueObjects/
│  │     └─ GroupSchedulingStrategy.cs   // 删除
│  ├─ ProviderAccounts/
│  │  └─ Entities/
│  │     └─ AccountToken.cs
│  └─ ApiKeys/
│     └─ Entities/
│        └─ ApiKeyProviderGroupBinding.cs
├─ src/AiRelay.Application/
│  ├─ ProviderGroups/
│  │  ├─ Dtos/
│  │  ├─ AppServices/
│  │  └─ Mappings/
│  ├─ ProviderAccounts/
│  │  ├─ Dtos/
│  │  └─ AppServices/
│  └─ ApiKeys/
│     ├─ Dtos/
│     └─ AppServices/
├─ src/AiRelay.Api/
│  └─ Controllers/
│     └─ ProviderGroupController.cs
└─ src/AiRelay.Infrastructure/
   └─ Persistence/
      ├─ EntityConfigurations/
      └─ Migrations/
```

### 2.3 目标模型

#### 分组层

`ProviderGroup` 简化为：

1. `id`
2. `name`
3. `description`
4. `enableStickySession`
5. `stickySessionExpirationHours`
6. `rateMultiplier`

移除：

1. `schedulingStrategy`

#### 账户层

`AccountToken` 增加：

1. `priority`
2. `weight`

含义：

1. `priority` 值越小优先级越高
2. `weight` 为同优先级内的分配权重

#### 分组账户关系层

`ProviderGroupAccountRelation` 简化为纯关系表：

1. `providerGroupId`
2. `accountTokenId`
3. `isActive`

移除：

1. `priority`
2. `weight`

#### 订阅绑定层

`ApiKeyProviderGroupBinding` 保持：

1. `apiKeyId`
2. `providerGroupId`
3. `priority`

### 2.4 核心交互策略

#### A. 分组管理页

目标：从“调度配置页”收敛成“分组池管理页”。

保留：

1. 分组列表
2. 新增和编辑分组
3. 删除分组
4. 查看该分组下已有账户数、支持路由、订阅绑定情况

移除：

1. 分组弹窗中的账户选择
2. 策略切换
3. 依赖策略的权重和优先级输入区
4. 大段策略说明文案

新增约束：

1. `default` 分组不可删除
2. `default` 分组名称不可编辑
3. `default` 分组始终存在

#### B. 账户管理页

目标：新增账户时就完成归组设置。

新增交互：

1. 在账户新增和编辑弹窗增加“所属分组”
2. 使用 PrimeNG 官方 `p-autoComplete` 多选模式
3. 默认预选 `default`
4. 支持多选多个分组

建议交互：

1. 输入关键词搜索分组
2. 候选项展示分组名、描述、是否默认分组
3. 已选分组以 chip 或 tag 显示
4. 至少保留一个分组

#### C. 订阅管理页

目标：绑定逻辑不变，但交互从“裸列表编辑”优化为“带顺序语义的绑定链”。

建议交互：

1. 上方使用 `p-autoComplete` 搜索分组
2. 选中后加入绑定列表
3. 列表按优先级排序显示：
   1. 第一项为主分组
   2. 第二项起为 fallback 分组
4. 支持上移、下移、删除
5. 每项展示分组名、说明、支持路由协议、账户数量

### 2.5 核心实践代码

#### 2.5.1 前端：账户编辑中使用 AutoComplete 绑定分组

```ts
type ProviderGroupOption = {
  id: string;
  name: string;
  description?: string;
  isDefault?: boolean;
};

selectedGroups = signal<ProviderGroupOption[]>([]);
filteredGroups = signal<ProviderGroupOption[]>([]);

searchGroups(event: { query: string }) {
  const keyword = event.query.trim().toLowerCase();
  this.filteredGroups.set(
    this.allGroups().filter(group =>
      group.name.toLowerCase().includes(keyword) ||
      (group.description ?? '').toLowerCase().includes(keyword)
    )
  );
}
```

```html
<p-autocomplete
  [suggestions]="filteredGroups()"
  [multiple]="true"
  [dropdown]="true"
  optionLabel="name"
  [ngModel]="selectedGroups()"
  (ngModelChange)="selectedGroups.set($event)"
  (completeMethod)="searchGroups($event)">
  <ng-template let-group pTemplate="item">
    <div class="flex items-center justify-between w-full">
      <span>{{ group.name }}</span>
      @if (group.isDefault) {
        <p-tag value="默认" severity="info" />
      }
    </div>
  </ng-template>
</p-autocomplete>
```

#### 2.5.2 后端：分组实体移除调度策略

```csharp
public class ProviderGroup : FullAuditedEntity<Guid>
{
    public string Name { get; private set; }
    public string? Description { get; private set; }
    public bool EnableStickySession { get; private set; }
    public int StickySessionExpirationHours { get; private set; } = 1;
    public decimal RateMultiplier { get; private set; } = 1.0m;

    public void Update(string name, string? description, decimal rateMultiplier)
    {
        Name = name;
        Description = description;
        RateMultiplier = rateMultiplier;
    }
}
```

#### 2.5.3 后端：账户实体承载优先级和权重

```csharp
public class AccountToken : FullAuditedEntity<Guid>
{
    public int Priority { get; private set; } = 0;
    public int Weight { get; private set; } = 1;

    public void UpdateScheduling(int priority, int weight)
    {
        Priority = priority;
        Weight = weight;
    }
}
```

#### 2.5.4 后端：默认分组初始化

```csharp
public async Task EnsureDefaultProviderGroupAsync(CancellationToken cancellationToken = default)
{
    var exists = await providerGroupRepository.AnyAsync(x => x.Name == "default", cancellationToken);
    if (exists)
    {
        return;
    }

    var group = new ProviderGroup(
        name: "default",
        description: "系统默认分组",
        enableStickySession: true,
        stickySessionExpirationHours: 1,
        rateMultiplier: 1.0m);

    await providerGroupRepository.InsertAsync(group, cancellationToken);
}
```

#### 2.5.5 后端：账户与分组关系改为纯归属关系

```csharp
public class ProviderGroupAccountRelation : DeletionAuditedEntity<Guid>
{
    public Guid ProviderGroupId { get; private set; }
    public Guid AccountTokenId { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ProviderGroupAccountRelation(Guid providerGroupId, Guid accountTokenId)
    {
        Id = Guid.CreateVersion7();
        ProviderGroupId = providerGroupId;
        AccountTokenId = accountTokenId;
    }
}
```

### 2.6 关键取舍说明

#### 为什么 `default` 分组要内建

因为它把系统从“强配置启动”变成“可直接启动”。

#### 为什么移除多调度策略

因为当前复杂度主要由策略分支带来，而不是由分组数量带来。

#### 为什么权重和优先级迁到账户

因为这两个属性更像账户能力属性，而不是某个账户在某个分组中的临时属性。

#### 为什么订阅绑定不能直接退化成普通多选

因为 fallback 的本质是有顺序的路由链，不是集合。

---

## 3、具体的实施步骤

## 第一阶段：先完成前端及 Mock: `frontend/_mock`

### 3.1 前端模型调整

修改文件：

1. [provider-group.dto.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/models/provider-group.dto.ts)
2. [account-token.dto.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/models/account-token.dto.ts)
3. `subscription.dto.ts`

调整内容：

1. 删除 `GroupSchedulingStrategy` 枚举及相关描述常量
2. 从 `ProviderGroupOutputDto`、`CreateProviderGroupInputDto`、`UpdateProviderGroupInputDto` 中移除：
   1. `schedulingStrategy`
   2. `accounts`
3. 在 `AccountToken` DTO 中新增：
   1. `priority`
   2. `weight`
   3. `providerGroupIds` 或 `providerGroups`
4. 订阅绑定 DTO 保持 `priority + providerGroupId` 不变

### 3.2 分组页面简化

修改文件：

1. [provider-group.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/components/provider-group/provider-group.ts)
2. [provider-group.html](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/components/provider-group/provider-group.html)
3. [group-edit-dialog.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/components/provider-group/widgets/group-edit-dialog/group-edit-dialog.ts)
4. `group-edit-dialog.html`
5. `group-table.ts`
6. `group-table.html`

实施点：

1. 删除分组编辑弹窗中的账户选择与账户表格
2. 删除策略选择器、策略说明、权重和优先级列
3. 分组列表只展示：
   1. 名称
   2. 描述
   3. 账户数
   4. 支持路由
   5. Sticky Session
   6. 倍率
4. `default` 分组增加只读标识
5. 删除按钮对 `default` 分组置灰

### 3.3 账户页面增强

修改文件：

1. [account-token.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/components/account-token/account-token.ts)
2. `account-edit-dialog.ts/html`
3. [account-token.dto.ts](/E:/workspace/ai/leistd-ai-relay/ai-relay/frontend/src/app/features/platform/models/account-token.dto.ts)

实施点：

1. 在账户新增和编辑弹窗中新增：
   1. 优先级
   2. 权重
   3. 所属分组
2. 表单按“基本信息 / 分组与调度 / 鉴权配置 / 模型配置 / 扩展属性”分区，降低单屏信息噪音
3. 账户列表新增：
   1. 所属分组列，按集合展示，不表达 fallback 语义
   2. 分组筛选器
   3. 调度状态区内展示优先级、权重、并发
4. 所属分组使用 PrimeNG `p-autoComplete` 多选模式
5. 默认勾选 `default`
6. 若用户清空所有分组，前端阻止提交

### 3.4 订阅管理页简化绑定体验

修改文件：

1. `subscription-edit-dialog.ts/html`
2. `subscription.dto.ts`

实施点：

1. 保留绑定列表
2. 将新增绑定改成：
   1. `p-autoComplete` 搜索分组
   2. 选择后插入绑定列表
3. 列表中明确显示：
   1. 主分组
   2. Fallback 1
   3. Fallback 2
4. 已绑定分组卡片采用三段式布局：
   1. 左侧为链路身份与分组标题
   2. 中部为协议标签与元信息
   3. 右侧为排序与删除操作
5. 描述通过标题右侧图标 hover 展示，协议与“账户数 / 费率 / 粘性会话超时”允许自适应换行，避免内容溢出
6. 提供上移和下移按钮，而不是让用户手工输入优先级数字
7. 提交时前端自动按顺序映射为 `priority = index + 1`

### 3.5 Mock 同步调整

修改文件：

1. `frontend/_mock/data/provider-group.ts`
2. `frontend/_mock/data/account-token.ts`
3. `frontend/_mock/data/api-key.ts`
4. `frontend/_mock/api/provider-group.ts`
5. `frontend/_mock/api/account-token.ts`
6. `frontend/_mock/api/api-key.ts`

实施点：

1. 初始化内置 `default` 分组
2. 删除 Mock 分组中的多策略数据
3. 账户 Mock 增加：
   1. `priority`
   2. `weight`
   3. `providerGroupIds`
4. 新增账户默认加入 `default`
5. 订阅绑定 Mock 保持顺序语义

## 第二阶段：在完成后端开发

### 3.6 领域模型调整

修改文件：

1. [ProviderGroup.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Domain/ProviderGroups/Entities/ProviderGroup.cs)
2. [ProviderGroupAccountRelation.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Domain/ProviderGroups/Entities/ProviderGroupAccountRelation.cs)
3. `AccountToken.cs`
4. `GroupSchedulingStrategy.cs` 删除
5. `GroupSchedulingStrategyFactory.cs` 删除
6. 相关策略实现删除

实施点：

1. `ProviderGroup` 删除 `SchedulingStrategy`
2. `ProviderGroupAccountRelation` 删除 `Priority/Weight`
3. `AccountToken` 增加 `Priority/Weight`
4. 清理所有按 `GroupSchedulingStrategy` 分支的领域逻辑

### 3.7 应用层与 DTO 调整

修改文件：

1. `ProviderGroupOutputDto.cs`
2. `CreateProviderGroupInputDto.cs`
3. `UpdateProviderGroupInputDto.cs`
4. `AccountTokenOutputDto.cs`
5. `CreateAccountTokenInputDto.cs`
6. `UpdateAccountTokenInputDto.cs`
7. `ApiKeyBindGroupInputDto.cs`
8. [ApiKeyBindingOutputDto.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Application/ApiKeys/Dtos/ApiKeyBindingOutputDto.cs)
9. `ProviderGroupProfile.cs`
10. `ApiKeyProfile.cs`

实施点：

1. 删除分组 DTO 中的策略字段和账户列表输入
2. 账户 DTO 增加优先级、权重、分组列表
3. 订阅绑定 DTO 保持不变，但支持前端排序提交

### 3.8 API 与应用服务调整

修改文件：

1. [ProviderGroupController.cs](/E:/workspace/ai/leistd-ai-relay/ai-relay/backend/src/AiRelay.Api/Controllers/ProviderGroupController.cs)
2. `ProviderGroupAppService.cs`
3. `AccountTokenAppService.cs`
4. `ApiKeyAppService.cs`

实施点：

1. 分组新增和编辑接口只处理分组元数据
2. 账户新增和编辑接口同时处理分组归属
3. 订阅绑定接口维持现状，只收按顺序排好的绑定列表

### 3.9 初始化与默认数据

实施点：

1. 在系统初始化流程中确保存在 `default` 分组
2. 新建账户时如果前端未传分组，后端兜底绑定到 `default`
3. 禁止删除 `default` 分组
4. 删除非默认分组前如存在订阅绑定，给出明确阻止提示

### 3.10 数据迁移策略

实施点：

1. 新增 `AccountToken.Priority` 和 `Weight` 列
2. 从 `ProviderGroupAccountRelation` 迁移历史优先级和权重
3. 删除 `ProviderGroup.SchedulingStrategy`
4. 删除 `ProviderGroupAccountRelation.Priority` 和 `Weight`
5. 生成 migration 并更新 snapshot

迁移约束建议：

1. 若同一账户在多个分组中存在不同优先级和权重，需要定义唯一迁移规则
2. 建议以 `default` 分组或首个关系为准，并在 migration 说明中明确

### 3.11 建议实施顺序

1. 先做前端模型和 Mock 收敛
2. 再做分组页简化
3. 再做账户页“归组 + 优先级/权重”
4. 再做订阅页 fallback 体验优化
5. 前端确认后，再开始后端实体、DTO、API、migration 改造
6. 最后清理废弃策略代码与常量、pipe、文案

---

## 最终建议

这套方案总体成立，而且比当前实现更符合运营实践。最关键的落点不是页面更简单，而是职责重新分配：

1. 分组页只管池子
2. 账户页只管账户能力和归属
3. 订阅页只管主备链路

后续实施建议严格按“两阶段推进”：

1. 第一阶段先做前端和 Mock，快速验证交互是否真正变简单
2. 第二阶段再推进后端收敛，避免前后端一起大改导致验证成本上升
