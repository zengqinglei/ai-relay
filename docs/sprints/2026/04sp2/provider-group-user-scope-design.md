# 分组用户范围控制方案

## 需求

### 1. 业务目标

围绕“分组管理、工作区聊天、订阅绑定分组”三条链路，引入分组的用户范围控制能力，满足以下业务规则：

1. 分组支持“公开”与“专属”两种可见范围。
2. 分组管理页的新增、编辑弹窗支持将分组分配给指定用户；未分配用户时视为公开分组。
3. 分组管理页的表格需要明确标识分组范围，至少能区分“公开”“专属”。
4. `/workspace` 聊天页面与“添加订阅”中可选分组的数据范围应统一为：
   - 公开分组
   - 分配给当前登录用户的专属分组
5. 聊天页面的分组选择需要降低识别门槛，默认体现“自动”“全部”的语义，而不是强迫用户先理解后台的分组概念。

### 2. 现状分析

#### 2.1 分组管理前端现状

当前分组管理页采用如下结构：

```text
frontend/src/app/features/platform/components/provider-group/
├─ provider-group.ts                      # 页面容器
├─ provider-group.html
└─ widgets/
   ├─ group-table/
   │  ├─ group-table.ts                   # 表格展示
   │  └─ group-table.html
   └─ group-edit-dialog/
      ├─ group-edit-dialog.ts             # 新增/编辑弹窗
      └─ group-edit-dialog.html
```

当前 `GroupEditDialogComponent` 表单字段仅覆盖：

1. `name`
2. `description`
3. `enableStickySession`
4. `stickySessionExpirationHours`
5. `rateMultiplier`

当前 `ProviderGroupOutputDto` 未包含任何用户范围字段，表格也仅展示“默认”状态，没有“公开/专属”的领域语义。

#### 2.2 工作区聊天前端现状

聊天页当前通过 `providerGroupService.getAll()` 一次性加载分组，并直接把返回结果用于：

1. 会话配置中的分组选择
2. 新会话默认分组
3. 模型选项加载

当前页面逻辑仍以“必须先选中一个真实分组 ID”为前提，缺少“自动 / 全部可见分组”的抽象层，不利于普通用户理解。

#### 2.3 订阅绑定分组前端现状

订阅编辑弹窗通过 `groupService.getGroups({ offset: 0, limit: 1000 })` 拉取全部分组数据，并作为自动完成候选源。当前页面没有前端权限裁剪逻辑，完全依赖接口返回值。

#### 2.4 `_mock` 现状

`frontend/_mock` 中已存在分组、聊天、订阅的联动模拟，但当前“可见分组”判断逻辑与新需求不一致：

1. `frontend/_mock/data/provider-group.ts` 没有 `AssignedUserId`、`AssignedUsername` 等字段。
2. `frontend/_mock/api/provider-group.ts` 当前更偏向“通过订阅绑定关系反推当前可见分组”，不符合“公开 + 当前用户专属”的新规则。
3. 这意味着 `_mock` 必须优先改造，否则前端页面联调行为会持续偏离真实后端方案。

#### 2.5 后端模块现状

分组后端模块已经具备清晰的分层结构，适合作为本次开发标准：

```text
backend/src/
├─ AiRelay.Api/
│  └─ Controllers/
│     └─ ProviderGroupController.cs
├─ AiRelay.Application/
│  └─ ProviderGroups/
│     ├─ AppServices/
│     │  ├─ IProviderGroupAppService.cs
│     │  └─ ProviderGroupAppService.cs
│     ├─ Dtos/
│     │  ├─ CreateProviderGroupInputDto.cs
│     │  ├─ UpdateProviderGroupInputDto.cs
│     │  ├─ ProviderGroupOutputDto.cs
│     │  └─ GetProviderGroupPagedInputDto.cs
│     └─ Mappings/
│        └─ ProviderGroupProfile.cs
├─ AiRelay.Domain/
│  └─ ProviderGroups/
│     ├─ Entities/
│     │  └─ ProviderGroup.cs
│     ├─ Repositories/
│     │  └─ IProviderGroupRepository.cs
│     └─ DomainServices/
│        └─ ProviderGroupDomainService.cs
└─ AiRelay.Infrastructure/
   └─ Persistence/
      └─ Repositories/
         └─ ProviderGroupRepository.cs
```

现有命名和分层已经较稳定，后续实现应继续遵循：

1. `Controller -> AppService -> DomainService/Repository -> Entity`
2. DTO 优先使用 `record`
3. Application / Domain 层优先使用主构造函数风格
4. 权限和当前用户范围判断优先在 Application/Repository 收口，而不是散落在前端页面

## 核心策略

### 1. 核心设计原则

#### 1.1 分组范围语义以“AssignedUserId 可空”建模

不建议单独新增 `IsPublic` 布尔字段作为主状态。更稳妥的方式是直接引入：

1. `AssignedUserId: Guid?`
2. `AssignedUsername: string?` 仅作为 DTO 输出摘要字段，不作为实体主字段

规则如下：

1. `AssignedUserId == null`：公开分组
2. `AssignedUserId != null`：专属分组，且仅该用户可见

这样可避免 `IsPublic + AssignedUserId` 双字段组合带来的状态冲突。

#### 1.2 数据权限在后端统一收口

“工作区聊天页可选分组”“订阅绑定可选分组”必须由后端统一按当前用户过滤，前端只负责展示，不负责权限裁剪。

统一规则：

1. 管理页分组列表：
   - 管理员默认可见全部分组
   - 非管理员可见公开分组 + 自己的专属分组
2. 工作区聊天、工作区订阅绑定：
   - 一律仅返回公开分组 + 当前用户专属分组

#### 1.3 UI 层提供“自动”抽象，不把魔法值写入数据库

聊天页的“自动”建议只存在于前端会话配置层，不直接作为真实 `ProviderGroupId` 落库。推荐策略：

1. UI 提供虚拟选项：`AUTO_GROUP_VALUE = '__auto__'`
2. 用户选择“自动”时：
   - 会话配置层显示为“自动（全部可用分组）”
   - 模型列表按“当前用户可见分组全集”聚合
3. 真正发送消息时：
   - 如果用户最终选择了具体模型，则由后端根据模型与分组归属解析路由
   - 如果现有链路仍强依赖 `ProviderGroupId`，则由应用层在“自动”场景下解析默认路由分组，而不是将 `__auto__` 持久化

这样既能降低识别门槛，又不会污染领域数据。

### 2. 调整内容树形目录结构

#### 2.1 前端

```text
frontend/
├─ _mock/
│  ├─ api/
│  │  ├─ provider-group.ts                # 调整可见分组过滤
│  │  ├─ subscription.ts                  # 复用新的分组可见范围
│  │  └─ workspace-chat.ts                # 支持自动/全部分组语义
│  └─ data/
│     ├─ provider-group.ts                # 新增 AssignedUserId/AssignedUsername
│     └─ user.ts                          # 复用现有用户数据做分配候选
├─ src/app/features/platform/
│  ├─ models/provider-group.dto.ts        # 增加范围字段
│  ├─ services/provider-group-service.ts  # 增加用户范围查询参数封装
│  └─ components/provider-group/
│     └─ widgets/
│        ├─ group-table/                  # 增加公开/专属标记
│        └─ group-edit-dialog/            # 增加用户分配控件
└─ src/app/features/workspace/
   ├─ components/chat/                    # 分组选择改为自动优先
   └─ ...
```

#### 2.2 后端

```text
backend/src/
├─ AiRelay.Domain/
│  └─ ProviderGroups/
│     ├─ Entities/ProviderGroup.cs
│     ├─ Repositories/IProviderGroupRepository.cs
│     └─ DomainServices/ProviderGroupDomainService.cs
├─ AiRelay.Application/
│  └─ ProviderGroups/
│     ├─ Dtos/
│     │  ├─ CreateProviderGroupInputDto.cs
│     │  ├─ UpdateProviderGroupInputDto.cs
│     │  ├─ ProviderGroupOutputDto.cs
│     │  ├─ ProviderGroupUserOptionDto.cs          # 可选：分配用户候选项
│     │  └─ GetProviderGroupPagedInputDto.cs
│     ├─ AppServices/ProviderGroupAppService.cs
│     └─ Mappings/ProviderGroupProfile.cs
├─ AiRelay.Infrastructure/
│  ├─ Persistence/Repositories/ProviderGroupRepository.cs
│  └─ Persistence/Migrations/                  # 增加字段迁移
└─ AiRelay.Api/
   └─ Controllers/ProviderGroupController.cs
```

### 3. 数据模型与接口契约调整

#### 3.1 Domain Entity 调整

`ProviderGroup` 实体新增：

```csharp
public Guid? AssignedUserId { get; private set; }
```

同时补充领域行为：

1. `AssignToUser(Guid userId)`
2. `SetPublic()`
3. `IsPublic => AssignedUserId is null`

必要时在领域服务中增加校验：

1. 被分配用户必须存在
2. 默认分组与专属分组之间是否允许并存，需要在业务层明确
3. 分组若已绑定关键资源，变更归属时是否需要额外校验

#### 3.2 Application DTO 调整

建议调整如下：

```csharp
public sealed record CreateProviderGroupInputDto(
    string Name,
    string? Description,
    bool EnableStickySession,
    int? StickySessionExpirationHours,
    decimal RateMultiplier,
    Guid? AssignedUserId
);

public sealed record UpdateProviderGroupInputDto(
    string Name,
    string? Description,
    bool EnableStickySession,
    int? StickySessionExpirationHours,
    decimal RateMultiplier,
    Guid? AssignedUserId
);

public sealed record ProviderGroupOutputDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsDefault,
    bool EnableStickySession,
    int? StickySessionExpirationHours,
    decimal RateMultiplier,
    DateTime CreationTime,
    Guid? AssignedUserId,
    string? AssignedUsername,
    bool IsPublic,
    string ScopeType,
    int AccountCount,
    IReadOnlyList<string> SupportedRouteProfiles
);
```

如果现有项目 DTO 已统一使用属性式 `record`，则保持现有风格即可，不强行切换位置参数 `record`。

#### 3.3 查询输入 DTO 调整

建议在 `GetProviderGroupPagedInputDto` 中增加范围过滤能力，为管理页预留扩展：

```csharp
public sealed record GetProviderGroupPagedInputDto : PagedRequestDto
{
    public string? Keyword { get; init; }
    public Guid? AssignedUserId { get; init; }
    public bool? IsPublic { get; init; }
    public bool? OnlyCurrentUserVisible { get; init; }
}
```

其中：

1. 管理页列表可按“公开 / 专属 / 指定用户”筛选
2. 工作区、订阅绑定不直接暴露该 DTO，而是走专门的“可见分组”查询接口，避免前端误用

#### 3.4 API 契约建议

建议增加或明确两个层次的查询接口：

1. 管理接口：返回管理视角列表
   - `GET /api/v1/provider-groups`
2. 工作区可见接口：返回当前用户可见列表
   - `GET /api/v1/provider-groups/visible`

`/visible` 返回规则固定为：

1. 公开分组
2. `AssignedUserId == CurrentUserId` 的分组

这样比让工作区继续复用“管理列表接口 + 前端自行筛选”更安全。

### 4. 前端页面策略

#### 4.1 分组管理页

##### 4.1.1 表格补充范围标记

在 `GroupTable` 中新增“范围”展示，建议使用 PrimeNG `p-tag`：

1. 公开：`severity="info"` 或 `severity="contrast"`，文案 `公开`
2. 专属：`severity="warn"` 或 `severity="success"`，文案 `专属`

如果是专属分组，可在副文本中展示用户名，例如：

1. 第一行：分组名称
2. 第二行：`专属 · 张三`

但表格仍保持单元格紧凑，避免拉高行高。

##### 4.1.2 新增/编辑弹窗支持用户分配

建议在 `GroupEditDialogComponent` 中新增“分配给用户”控件，优先使用 PrimeNG v21 官方能力：

1. 优先选型：`p-autocomplete`
2. 原因：
   - 用户数量可能增长，不适合纯 `p-select`
   - 可输入模糊搜索用户名
   - 与现有订阅绑定分组的交互模型一致，维护成本低

布局建议：

1. 基础字段区域不变
2. 在 `description` 下方加入“分配用户”控件
3. 提供一个明确的空态文案：`未分配则为公开分组`
4. 允许清空已选用户，清空即表示公开

样式上优先用 PrimeNG `fluid`、`size="small"`、Tailwind 间距类控制，不在全局 `styles.css` 添加新组件覆盖样式。

#### 4.2 工作区聊天页

##### 4.2.1 分组选择改为“自动优先”

建议把当前分组下拉改造成以下结构：

1. 默认项：`自动`
2. 分隔语义后的真实选项：
   - `全部可见分组`
   - 公开分组列表
   - 我的专属分组列表

若当前 PrimeNG `p-select` 不便直接做分组展示，可采用折中方案：

1. 默认选项：`自动`
2. 真实分组选项直接平铺
3. 每个分组名称后追加范围提示，例如：
   - `Claude 分组 · 公开`
   - `我的专属分组 · 专属`

交互规则：

1. 首次进入页面默认 `自动`
2. `自动` 时模型列表来自“当前用户所有可见分组`
3. 用户手工选择具体分组后，模型列表再收敛到该分组

##### 4.2.2 模型选择联动

当分组为 `自动` 时：

1. 模型列表应去重展示
2. 必要时在 option 副文案中显示来源分组，避免同名模型混淆
3. 后端若暂时不支持“按可见分组全集聚合模型”，则可以先由前端对多个分组结果做轻量聚合，但最终仍建议后端提供聚合接口

#### 4.3 订阅编辑弹窗

“绑定分组”候选源统一切换到“当前用户可见分组”接口，不再直接依赖管理列表接口。

这样可以确保：

1. 工作区与订阅绑定的权限口径一致
2. 后续若专属分组继续扩展，前端无需再同步维护权限逻辑

### 5. `_mock` 优先适配策略

本次改造必须先做 `_mock`，否则前端界面很难在本地稳定验证。

#### 5.1 数据层

优先扩展 `frontend/_mock/data/provider-group.ts`：

1. 为每个分组增加 `assignedUserId`
2. 为展示方便补充 `assignedUsername`
3. 同时准备公开、当前用户专属、其他用户专属三类样本

建议至少构造以下数据：

1. `assignedUserId = null` 的公开分组
2. `assignedUserId = currentUserId` 的当前用户专属分组
3. `assignedUserId = anotherUserId` 的其他用户专属分组

#### 5.2 API 层

`frontend/_mock/api/provider-group.ts` 需要新增或重构以下逻辑：

1. 管理列表接口：管理员看全部，普通用户看公开 + 自己专属
2. 可见分组接口：固定只返回公开 + 自己专属
3. 创建、更新接口：支持传入 `assignedUserId`

`frontend/_mock/api/subscription.ts` 与 `frontend/_mock/api/workspace-chat.ts` 要同步复用“可见分组”过滤函数，而不是各自维护一套逻辑。

#### 5.3 前端联调顺序

先让 `_mock` 跑通以下场景：

1. 分组管理新增公开分组
2. 分组管理新增专属分组
3. 聊天页只能看到公开 + 当前用户专属
4. 订阅绑定弹窗只能搜索到公开 + 当前用户专属

确认交互后，再同步真实后端。

### 6. 核心实现代码

#### 6.1 Domain Entity 示例

```csharp
public class ProviderGroup : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public Guid? AssignedUserId { get; private set; }

    public bool IsPublic => AssignedUserId is null;

    public void AssignToUser(Guid userId)
    {
        AssignedUserId = userId;
    }

    public void SetPublic()
    {
        AssignedUserId = null;
    }
}
```

#### 6.2 Repository 查询示例

```csharp
public async Task<List<ProviderGroup>> GetVisibleGroupsAsync(Guid currentUserId, CancellationToken cancellationToken = default)
{
    return await (await GetQueryableAsync())
        .Where(x => x.AssignedUserId == null || x.AssignedUserId == currentUserId)
        .OrderByDescending(x => x.IsDefault)
        .ThenBy(x => x.Name)
        .ToListAsync(cancellationToken);
}
```

#### 6.3 AppService 输出组装示例

```csharp
public async Task<IReadOnlyList<ProviderGroupOutputDto>> GetVisibleAsync(CancellationToken cancellationToken)
{
    var currentUserId = CurrentUser.GetId();
    var groups = await providerGroupRepository.GetVisibleGroupsAsync(currentUserId, cancellationToken);
    var userMap = await userLookupService.GetUserNameMapAsync(groups
        .Where(x => x.AssignedUserId.HasValue)
        .Select(x => x.AssignedUserId!.Value)
        .Distinct(),
        cancellationToken);

    return groups.Select(x => new ProviderGroupOutputDto(
        x.Id,
        x.Name,
        x.Description,
        x.IsDefault,
        x.EnableStickySession,
        x.StickySessionExpirationHours,
        x.RateMultiplier,
        x.CreationTime,
        x.AssignedUserId,
        x.AssignedUserId is Guid userId ? userMap.GetValueOrDefault(userId) : null,
        x.AssignedUserId is null,
        x.AssignedUserId is null ? "Public" : "Private",
        x.AccountCount,
        x.SupportedRouteProfiles
    )).ToList();
}
```

#### 6.4 前端表单示例

```ts
readonly form = this.fb.nonNullable.group({
  name: ['', [Validators.required, Validators.maxLength(100)]],
  description: [''],
  assignedUserId: [null as string | null],
  enableStickySession: [false],
  stickySessionExpirationHours: [null as number | null],
  rateMultiplier: [1]
});
```

```html
<div class="flex flex-col gap-2">
  <label for="assignedUserId" class="text-sm font-medium">分配给用户</label>
  <p-autocomplete
    inputId="assignedUserId"
    [suggestions]="userOptions()"
    optionLabel="userName"
    optionValue="id"
    [forceSelection]="true"
    [dropdown]="true"
    [fluid]="true"
    size="small"
    placeholder="不分配则为公开分组"
    (completeMethod)="searchUsers($event)"
    formControlName="assignedUserId" />
</div>
```

#### 6.5 聊天页“自动”选项示例

```ts
const AUTO_GROUP_VALUE = '__auto__';

readonly providerGroupOptions = computed(() => {
  const groups = this.providerGroups();

  return [
    { label: '自动', value: AUTO_GROUP_VALUE, hint: '从全部可见分组自动匹配模型' },
    ...groups.map(group => ({
      label: group.isPublic ? `${group.name} · 公开` : `${group.name} · 专属`,
      value: group.id
    }))
  ];
});
```

### 7. 开发规范落地要求

#### 7.1 前端规范

以分组管理页作为本次开发标准样板：

1. 页面容器负责数据装配与状态编排
2. `widgets/` 内的 table/dialog 只承接单一职责
3. DTO、service、view model 命名沿用现有 `provider-group.*` 结构，不新增含糊缩写
4. 新增布局优先使用 PrimeNG v21 组件能力与 TailwindCSS v4 工具类
5. 样式优先收敛到当前组件 `.css` 或模板原子类，不在全局 `styles.css` 做组件级覆盖

#### 7.2 后端规范

以分组模块作为本次实现标准：

1. 输入输出 DTO 优先 `record`
2. AppService、DomainService 优先主构造函数
3. 查询权限收口在 Application/Repository，不在 Controller 拼条件
4. 领域状态变更通过实体方法或 DomainService 执行，不直接散落在 AppService 中改字段
5. 迁移、仓储、DTO、映射要同步提交，避免“实体已改、接口未对齐”的半成品状态

## 实施计划

### 1. 第一阶段：`_mock` 优先适配

目标：先在前端本地完整跑通业务行为。

1. 扩展 `frontend/_mock/data/provider-group.ts`，补足公开/专属样本数据。
2. 调整 `frontend/_mock/api/provider-group.ts`，实现“公开 + 当前用户专属”的可见逻辑。
3. 同步改造 `frontend/_mock/api/subscription.ts`、`frontend/_mock/api/workspace-chat.ts`，统一复用可见分组过滤。
4. 验证聊天页、订阅绑定、分组管理三条链路的 mock 行为一致。

### 2. 第二阶段：前端结构与交互改造

1. 扩展 `provider-group.dto.ts` 与 `provider-group-service.ts`。
2. 分组管理表格增加范围标记与用户摘要。
3. 分组新增/编辑弹窗增加“分配给用户”控件。
4. 工作区聊天页增加“自动”选项与可见分组列表。
5. 订阅编辑弹窗改为只拉取可见分组接口。

### 3. 第三阶段：后端领域与接口改造

1. `ProviderGroup` 实体新增 `AssignedUserId`。
2. `Create/Update/Output` DTO 同步扩展。
3. `ProviderGroupAppService` 增加可见分组查询入口。
4. `ProviderGroupRepository` 增加“当前用户可见分组”查询实现。
5. 视项目现有用户查询方式补充 `AssignedUsername` 组装逻辑。
6. 增加数据库迁移并处理历史数据回填：
   - 旧数据默认回填为公开分组

### 4. 第四阶段：联调与回归

1. 分组管理：
   - 新增公开分组
   - 新增专属分组
   - 编辑公开转专属
   - 编辑专属转公开
2. 工作区聊天：
   - 默认自动
   - 自动下模型可选
   - 指定分组后模型正确收敛
   - 不可看到其他用户专属分组
3. 订阅绑定：
   - 仅能绑定公开分组与当前用户专属分组
4. 管理员与普通用户双角色回归：
   - 管理页列表权限
   - 工作区可见范围

### 5. 风险与处理建议

1. 风险：聊天链路当前可能强依赖真实 `ProviderGroupId`。
   - 处理：先把“自动”作为 UI 选项落地，后端保留解析默认分组能力，不将魔法值入库。
2. 风险：`AssignedUsername` 如果直接通过跨模块联查获取，可能引入额外耦合。
   - 处理：先在 Application 层通过用户查询服务做摘要映射，不把用户名冗余到领域实体。
3. 风险：历史数据没有范围信息。
   - 处理：迁移默认回填为公开，保证兼容旧行为。
4. 风险：`_mock` 与真实后端接口口径再次分叉。
   - 处理：先定义稳定的 `visible` 接口契约，再同步 mock 与真实实现。

### 6. 建议的开发顺序

1. `_mock` 数据与 API
2. 前端 DTO / service
3. 分组管理页 UI
4. 聊天页“自动”与可见分组
5. 订阅绑定可见分组
6. 后端实体 / DTO / 仓储 / AppService / Controller
7. 迁移与联调回归
