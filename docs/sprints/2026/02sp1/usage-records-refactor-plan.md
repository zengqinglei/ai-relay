# 使用记录页面重构方案

## 一、需求概述

### 1.1 前端调整
- **菜单重命名**：将"数据分析"改为"使用记录"
- **路由调整**：`/platform/analytics` → `/platform/usage-records`
- **页面功能**：展示使用记录的详细查询列表，支持多条件筛选、排序和分页

### 1.2 概览页面（Dashboard）
- 保持现有功能不变，继续展示统计图表和指标卡片

### 1.3 后端调整
- 审视现有接口，移除不再使用的代码
- 确保接口满足前端需求

---

## 二、前端重构方案

### 2.1 目录结构调整

```
frontend/src/app/features/platform/
├── components/
│   ├── dashboard/                          # 概览页面（保持不变）
│   │   ├── dashboard.ts
│   │   ├── dashboard.html
│   │   └── widgets/
│   │       ├── metrics-cards/
│   │       ├── usage-trend-chart/
│   │       ├── model-distribution-chart/
│   │       └── api-key-trend-chart/
│   │
│   ├── usage-records/                      # 【新建】使用记录页面（原 analytics）
│   │   ├── usage-records.ts                # 主组件
│   │   ├── usage-records.html              # 模板
│   │   └── widgets/
│   │       ├── usage-filter-panel/         # 【新建】筛选面板组件
│   │       │   ├── usage-filter-panel.ts
│   │       │   └── usage-filter-panel.html
│   │       └── usage-records-table/        # 【新建】记录表格组件
│   │           ├── usage-records-table.ts
│   │           └── usage-records-table.html
│   │
│   └── analytics/                          # 【删除】原数据分析页面
│       ├── analytics.ts                    # 【删除】
│       └── analytics.html                  # 【删除】
│
├── models/
│   ├── dashboard.dto.ts                    # 概览页面 DTO（保持不变）
│   ├── usage.dto.ts                        # 【扩展】添加使用记录列表相关 DTO
│   └── ...
│
├── services/
│   ├── dashboard-service.ts                # 概览页面服务（保持不变）
│   ├── usage-query.service.ts              # 【扩展】添加分页查询方法
│   └── ...
│
└── platform.routes.ts                      # 【修改】路由配置
```

### 2.2 页面设计

#### 2.2.1 使用记录页面布局

参考订阅管理页面的布局风格，采用以下结构：

```
┌─────────────────────────────────────────────────────────────┐
│  筛选面板（可折叠）                                           │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ API KEY名  [下拉选择]  请求模型 [下拉选择]            │  │
│  │ 供应商账户 [下拉选择]  分组     [下拉选择]            │  │
│  │ 平台类型   [下拉选择]  时间范围 [日期范围选择器]      │  │
│  │                                    [查询] [重置]       │  │
│  └───────────────────────────────────────────────────────┘  │
├─────────────────────────────────────────────────────────────┤
│  数据表格                                                     │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ 请求时间 ↓ │ API KEY │ 平台/分组 │ 模型 │ Token │ ... │  │
│  │ 2024-02-05 │ key-001 │ GEMINI/G1 │ ... │ ↑100  │ ... │  │
│  │ 14:30:25   │         │           │     │ ↓200  │     │  │
│  ├───────────────────────────────────────────────────────┤  │
│  │ ...                                                   │  │
│  └───────────────────────────────────────────────────────┘  │
│  [分页控件]                                                  │
└─────────────────────────────────────────────────────────────┘
```

#### 2.2.2 列设计（合并展示）

| 列名 | 展示内容 | 说明 |
|------|---------|------|
| **请求时间** | `2024-02-05 14:30:25` | 支持排序 |
| **API KEY** | `key-name-001` | 单行展示 |
| **平台/分组** | `GEMINI` <br> `分组A` | 两行展示：平台类型 + 分组名 |
| **供应商账户** | `account-001` | 单行展示 |
| **模型** | `gemini-2.0-flash-exp` | 单行展示 |
| **请求信息** | `/v1beta/models/...` <br> `流式 · Chrome/120` | 两行展示：路径 + 类型/UA |
| **Token** | `↑ 1,234` <br> `↓ 5,678` | 两行展示：输入↑ + 输出↓，支持排序 |
| **IP地址** | `192.168.1.100` | 单行展示 |
| **消耗金额** | `$0.0123` | 单行展示 |
| **状态** | `成功` / `失败` / `取消` | 带颜色标签 |
| **耗时** | `1,234 ms` | 支持排序 |
| **操作** | `[详情]` | 查看错误信息等 |

#### 2.2.3 筛选条件

- **API KEY名**：下拉选择（支持搜索）
- **请求模型**：下拉选择（支持搜索）
- **供应商账户**：下拉选择（支持搜索）
- **分组**：下拉选择
- **平台类型**：下拉选择（GEMINI、CLAUDE、OPENAI、ANTIGRAVITY）
- **时间范围**：日期范围选择器（默认今天）

#### 2.2.4 排序支持

- 请求时间（默认降序）
- Token（输入+输出总和）
- 耗时

---

## 三、后端调整方案

### 3.1 现有接口分析

#### 3.1.1 保留的接口（Dashboard 使用）

✅ **UsageQueryController**
- `GET /api/v1/usage/query/metrics` - 获取流量指标
- `GET /api/v1/usage/query/trend` - 获取流量趋势
- `GET /api/v1/usage/query/top-api-keys` - 获取 Top API Keys
- `GET /api/v1/usage/query/model-distribution` - 获取模型分布
- `GET /api/v1/usage/query/records` - 获取使用记录列表（已存在，满足需求）

#### 3.1.2 需要扩展的功能

当前 `UsageRecordListOutputDto` 已包含所需字段：
- ✅ 请求时间 (`CreationTime`)
- ✅ API KEY 名 (`ApiKeyName`)
- ✅ 平台类型 (`Platform`)
- ✅ 分组名 (`ProviderGroupName`)
- ✅ 供应商账户 (`AccountTokenName`)
- ✅ 请求模型 (`Model`)
- ✅ 请求路径 (`RequestPath`)
- ✅ 是否流式 (`IsStreaming`)
- ✅ User Agent (`UserAgent`)
- ✅ 输入/输出 Token (`InputTokens`, `OutputTokens`)
- ✅ 请求 IP (`RequestIp`)
- ✅ 消耗金额 (`FinalCost`)
- ✅ 状态 (`Status`)
- ✅ 耗时 (`ElapsedMilliseconds`)
- ✅ 错误信息 (`ErrorMessage`)

**结论**：后端接口已满足需求，无需修改。

#### 3.1.3 需要添加的辅助接口（可选）

为了支持前端下拉选择器的数据源，可以考虑添加以下接口：

```csharp
// 获取所有 API Key 名称列表（用于筛选下拉）
GET /api/v1/usage/query/api-key-names

// 获取所有模型名称列表（用于筛选下拉）
GET /api/v1/usage/query/model-names

// 获取所有供应商账户列表（用于筛选下拉）
GET /api/v1/usage/query/account-tokens

// 获取所有分组列表（用于筛选下拉）
GET /api/v1/usage/query/provider-groups
```

**或者**：前端直接使用现有的 `AccountTokenService` 和 `ProviderGroupService` 获取数据。

### 3.2 需要删除的代码

#### 3.2.1 后端

目前没有发现需要删除的后端代码，所有接口都在使用中。

#### 3.2.2 前端

- ❌ `frontend/src/app/features/platform/components/analytics/` 整个目录

---

## 四、实施步骤

### 4.1 前端实施步骤

#### Step 1: 创建新的使用记录页面组件

1. **创建主组件**
   - `usage-records/usage-records.ts`
   - `usage-records/usage-records.html`

2. **创建子组件**
   - `usage-records/widgets/usage-filter-panel/` - 筛选面板
   - `usage-records/widgets/usage-records-table/` - 数据表格

#### Step 2: 扩展 DTO 和 Service

1. **扩展 `usage.dto.ts`**
   ```typescript
   // 添加分页查询输入 DTO
   export interface UsageRecordPagedInputDto {
     apiKeyName?: string;
     model?: string;
     accountTokenId?: string;
     providerGroupId?: string;
     platform?: ProviderPlatform;
     startTime?: string;
     endTime?: string;
     sorting?: string;
     offset: number;
     limit: number;
   }

   // 添加列表输出 DTO
   export interface UsageRecordListOutputDto {
     id: string;
     creationTime: string;
     apiKeyName: string;
     platform: ProviderPlatform;
     providerGroupName: string;
     accountTokenName: string;
     model: string;
     requestPath: string;
     isStreaming: boolean;
     userAgent: string;
     inputTokens: number;
     outputTokens: number;
     cacheReadTokens?: number;
     cacheCreationTokens?: number;
     requestIp: string;
     finalCost: number;
     status: UsageStatus;
     elapsedMilliseconds?: number;
     errorMessage?: string;
   }

   // 添加枚举
   export enum ProviderPlatform {
     GEMINI_ACCOUNT = 'GEMINI_ACCOUNT',
     GEMINI_APIKEY = 'GEMINI_APIKEY',
     CLAUDE_ACCOUNT = 'CLAUDE_ACCOUNT',
     CLAUDE_APIKEY = 'CLAUDE_APIKEY',
     OPENAI_ACCOUNT = 'OPENAI_ACCOUNT',
     OPENAI_APIKEY = 'OPENAI_APIKEY',
     ANTIGRAVITY = 'ANTIGRAVITY'
   }

   export enum UsageStatus {
     InProgress = 0,
     Success = 1,
     Failed = 2,
     Cancelled = 3
   }
   ```

2. **扩展 `usage-query.service.ts`**
   ```typescript
   getPagedRecords(input: UsageRecordPagedInputDto): Observable<PagedResultDto<UsageRecordListOutputDto>> {
     const params = new HttpParams({ fromObject: input as any });
     return this.http.get<PagedResultDto<UsageRecordListOutputDto>>(
       `${this.baseUrl}/records`,
       { params }
     );
   }
   ```

#### Step 3: 更新路由配置

修改 `platform.routes.ts`：
```typescript
{
  path: 'usage-records',  // 原 'analytics'
  loadComponent: () => import('./components/usage-records/usage-records').then(m => m.UsageRecords)
}
```

#### Step 4: 更新侧边栏菜单

修改 `default-sidebar.ts`：
```typescript
private readonly platformMenuItems: MenuItem[] = [
  { label: '概览', icon: 'pi-home', route: '/platform' },
  { label: '账户管理', icon: 'pi-wallet', route: '/platform/account-tokens' },
  { label: '分组管理', icon: 'pi-box', route: '/platform/provider-groups' },
  { label: '订阅管理', icon: 'pi-shopping-cart', route: '/platform/subscriptions' },
  { label: '使用记录', icon: 'pi-list', route: '/platform/usage-records' },  // 修改
  { label: '系统设置', icon: 'pi-cog', route: '/platform/settings' }
];
```

#### Step 5: 删除旧的 analytics 组件

```bash
rm -rf frontend/src/app/features/platform/components/analytics/
```

### 4.2 后端实施步骤

#### Step 1: 审查现有代码

✅ 所有接口都在使用中，无需删除。

#### Step 2: （可选）添加辅助接口

如果需要为前端下拉选择器提供数据源，可以在 `UsageQueryController` 中添加：

```csharp
/// <summary>
/// 获取所有使用过的 API Key 名称（用于筛选）
/// </summary>
[HttpGet("api-key-names")]
public async Task<List<string>> GetApiKeyNamesAsync(CancellationToken cancellationToken)
{
    // 实现逻辑
}

/// <summary>
/// 获取所有使用过的模型名称（用于筛选）
/// </summary>
[HttpGet("model-names")]
public async Task<List<string>> GetModelNamesAsync(CancellationToken cancellationToken)
{
    // 实现逻辑
}
```

**建议**：前端直接使用现有的 `AccountTokenService` 和 `ProviderGroupService`，无需新增接口。

---

## 五、技术实现细节

### 5.1 筛选面板组件 (UsageFilterPanel)

**功能**：
- 支持多条件筛选
- 支持折叠/展开
- 支持重置

**使用的 PrimeNG 组件**：
- `p-select` - 下拉选择器
- `p-datepicker` - 日期范围选择器
- `p-button` - 按钮
- `p-panel` - 可折叠面板

**输入输出**：
```typescript
@Input() loading = false;
@Output() filterChange = new EventEmitter<UsageRecordPagedInputDto>();
```

### 5.2 数据表格组件 (UsageRecordsTable)

**功能**：
- 展示使用记录列表
- 支持排序（请求时间、Token、耗时）
- 支持分页
- 支持查看详情（错误信息）

**使用的 PrimeNG 组件**：
- `p-table` - 数据表格
- `p-paginator` - 分页器
- `p-tag` - 状态标签
- `p-dialog` - 详情对话框

**输入输出**：
```typescript
@Input() records: UsageRecordListOutputDto[] = [];
@Input() totalRecords = 0;
@Input() loading = false;
@Output() pageChange = new EventEmitter<{ offset: number; limit: number }>();
@Output() sortChange = new EventEmitter<string>();
```

### 5.3 样式规范

- 使用 Tailwind CSS 进行布局
- 使用 PrimeNG 默认主题
- 参考订阅管理页面的样式风格
- 响应式设计，支持移动端

---

## 六、测试计划

### 6.1 前端测试

1. **筛选功能测试**
   - 单条件筛选
   - 多条件组合筛选
   - 时间范围筛选
   - 重置功能

2. **表格功能测试**
   - 数据展示
   - 排序功能
   - 分页功能
   - 详情查看

3. **响应式测试**
   - 桌面端
   - 平板端
   - 移动端

### 6.2 后端测试

1. **接口测试**
   - 分页查询
   - 筛选条件
   - 排序功能
   - 性能测试（大数据量）

---

## 七、文件变更清单

### 7.1 前端文件变更

#### 新建文件
```
frontend/src/app/features/platform/components/usage-records/
├── usage-records.ts                                    # 主组件
├── usage-records.html                                  # 模板
└── widgets/
    ├── usage-filter-panel/
    │   ├── usage-filter-panel.ts                       # 筛选面板组件
    │   └── usage-filter-panel.html                     # 筛选面板模板
    └── usage-records-table/
        ├── usage-records-table.ts                      # 表格组件
        └── usage-records-table.html                    # 表格模板
```

#### 修改文件
```
frontend/src/app/features/platform/
├── models/usage.dto.ts                                 # 扩展 DTO
├── services/usage-query.service.ts                     # 扩展服务方法
├── platform.routes.ts                                  # 修改路由
└── ...

frontend/src/app/layout/components/default-sidebar/
└── default-sidebar.ts                                  # 修改菜单
```

#### 删除文件
```
frontend/src/app/features/platform/components/analytics/
├── analytics.ts                                        # 删除
└── analytics.html                                      # 删除
```

### 7.2 后端文件变更

#### 无需修改
- 现有接口已满足需求

#### 可选新增（如果需要辅助接口）
```
backend/src/AiRelay.Application/UsageRecords/AppServices/
└── UsageQueryAppService.cs                             # 添加辅助方法

backend/src/AiRelay.Api/Controllers/
└── UsageQueryController.cs                             # 添加辅助接口
```

---

## 八、完整目录树（调整后）

### 8.1 前端目录树

```
frontend/src/app/features/platform/
├── components/
│   ├── dashboard/                                      # 概览页面
│   │   ├── dashboard.ts
│   │   ├── dashboard.html
│   │   └── widgets/
│   │       ├── metrics-cards/
│   │       │   ├── metrics-cards.ts
│   │       │   └── metrics-cards.html
│   │       ├── usage-trend-chart/
│   │       │   ├── usage-trend-chart.ts
│   │       │   └── usage-trend-chart.html
│   │       ├── model-distribution-chart/
│   │       │   ├── model-distribution-chart.ts
│   │       │   └── model-distribution-chart.html
│   │       └── api-key-trend-chart/
│   │           ├── api-key-trend-chart.ts
│   │           └── api-key-trend-chart.html
│   │
│   ├── usage-records/                                  # 【新建】使用记录页面
│   │   ├── usage-records.ts
│   │   ├── usage-records.html
│   │   └── widgets/
│   │       ├── usage-filter-panel/                     # 【新建】筛选面板
│   │       │   ├── usage-filter-panel.ts
│   │       │   └── usage-filter-panel.html
│   │       └── usage-records-table/                    # 【新建】数据表格
│   │           ├── usage-records-table.ts
│   │           └── usage-records-table.html
│   │
│   ├── account-token/                                  # 账户管理
│   ├── provider-group/                                 # 分组管理
│   ├── subscriptions/                                  # 订阅管理
│   ├── settings/                                       # 系统设置
│   └── infrastructure/                                 # 基础设施
│
├── models/
│   ├── dashboard.dto.ts                                # 概览页面 DTO
│   ├── usage.dto.ts                                    # 【扩展】使用记录 DTO
│   ├── subscription.dto.ts
│   ├── account-token.dto.ts
│   └── provider-group.dto.ts
│
├── services/
│   ├── dashboard-service.ts                            # 概览页面服务
│   ├── usage-query.service.ts                          # 【扩展】使用记录查询服务
│   ├── subscription-service.ts
│   ├── subscription-metric-service.ts
│   ├── account-token-service.ts
│   ├── account-token-metric-service.ts
│   └── provider-group-service.ts
│
└── platform.routes.ts                                  # 【修改】路由配置
```

### 8.2 后端目录树

```
backend/src/
├── AiRelay.Api/
│   └── Controllers/
│       ├── UsageQueryController.cs                     # 使用记录查询控制器（保持不变）
│       ├── AccountTokenController.cs
│       ├── ProviderGroupController.cs
│       └── SubscriptionController.cs
│
├── AiRelay.Application/
│   ├── UsageRecords/
│   │   ├── AppServices/
│   │   │   ├── IUsageQueryAppService.cs
│   │   │   └── UsageQueryAppService.cs                 # 【可选扩展】添加辅助方法
│   │   └── Dtos/
│   │       ├── Query/
│   │       │   ├── UsageMetricsOutputDto.cs
│   │       │   ├── UsageTrendOutputDto.cs
│   │       │   ├── ApiKeyTrendOutputDto.cs
│   │       │   ├── ModelDistributionOutputDto.cs
│   │       │   ├── UsageRecordPagedInputDto.cs         # 已存在
│   │       │   └── UsageRecordListOutputDto.cs         # 已存在
│   │       └── Lifecycle/
│   │           ├── StartUsageInputDto.cs
│   │           ├── StartUsageOutputDto.cs
│   │           └── FinishUsageInputDto.cs
│   │
│   ├── AccountTokens/
│   ├── ProviderGroups/
│   └── Subscriptions/
│
└── AiRelay.Domain/
    ├── UsageRecords/
    │   └── Entities/
    │       ├── UsageRecord.cs                          # 实体（保持不变）
    │       └── ...
    └── ProviderAccounts/
        └── ValueObjects/
            ├── UsageStatus.cs                          # 枚举（保持不变）
            └── ProviderPlatform.cs                     # 枚举（保持不变）
```

---

## 九、总结

### 9.1 主要变更

1. **前端**
   - ✅ 将"数据分析"菜单改为"使用记录"
   - ✅ 创建新的使用记录页面，支持详细的查询和筛选
   - ✅ 删除旧的 analytics 组件
   - ✅ 概览页面保持不变

2. **后端**
   - ✅ 现有接口已满足需求，无需修改
   - ⚠️ 可选：添加辅助接口为前端下拉选择器提供数据源

### 9.2 优势

1. **性能优化**：使用冗余字段避免关联查询
2. **用户体验**：参考订阅管理页面的成熟布局
3. **可维护性**：清晰的组件划分和职责分离
4. **扩展性**：预留详情查看功能，方便后续扩展

### 9.3 注意事项

1. 所有文件命名遵循 Angular v20+ 规范
2. 使用 PrimeNG 官方组件，保持默认风格
3. 使用 Tailwind CSS 进行布局
4. 遵循前端开发规范文档
5. 确保响应式设计，支持移动端

---

## 十、待确认事项

1. ✅ 是否需要添加后端辅助接口（API Key 列表、模型列表等）？
   - **建议**：前端直接使用现有服务，无需新增

2. ✅ 是否需要支持导出功能（CSV/Excel）？
   - **建议**：后续迭代添加

3. ✅ 是否需要支持批量操作（批量删除等）？
   - **建议**：使用记录通常只读，不建议删除

4. ✅ 详情对话框需要展示哪些额外信息？
   - **建议**：主要展示完整的错误信息和请求详情

---

**方案制定完成，请审查确认后开始实施。**
