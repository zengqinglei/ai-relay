# Dashboard 概览页面重构方案

## 一、重构目标

### 1.1 前端调整
- ✅ 移除实时请求日志功能（仅前端移除，后端接口保留）
- ✅ 调整 Metric Cards 展示内容
- ✅ 将"流量趋势"改为"Token 使用趋势"
- ✅ 新增"模型使用分布"图表
- ✅ 将"实时请求日志"改为"最近使用（TOP 10）"
- ✅ 使用 PrimeNG 组件，遵循前端开发规范

### 1.2 后端调整
- ✅ 确保现有接口满足新需求
- ⚠️ **保留实时请求日志接口**（后续其他页面可能使用）

### 1.3 Mock 调整
- ✅ 移除实时请求日志 Mock（Dashboard不再使用）
- ✅ 新增模型分布 Mock 数据
- ✅ 新增 API Key 趋势 Mock 数据

---

## 二、详细设计

### 2.1 Metric Cards 调整

#### 原有设计（4个卡片）
1. **总请求数** - 保持不变
2. **活跃账户** - 需要替换
3. **活跃订阅** - 保持不变
4. **平均成功率** - 保持不变

#### 新设计（4个卡片）
1. **总请求数** ✅
   - 主指标：今日总请求数
   - 副指标：RPS（每秒请求数）
   - 趋势：相比昨天的百分比变化

2. **今日消耗** 🆕
   - 主指标：今日总金额（¥X.XXXX）
   - 副指标：输入Token / 输出Token
   - 趋势：相比昨天的百分比变化

3. **活跃订阅** ✅
   - 主指标：活跃订阅数 / 总订阅数
   - 副指标：即将过期数量
   - 进度条：活跃率百分比

4. **平均成功率** ✅
   - 主指标：成功率百分比
   - 副指标：异常请求数（24h）
   - 健康指示器：绿色/黄色/红色

---

### 2.2 图表区域调整

#### 原有设计
- **流量趋势**（左侧，占 100% 宽度）
- **实时请求日志**（下方，占 100% 宽度）

#### 新设计
```
┌─────────────────────────────────────────────────────────────┐
│  Metric Cards (4个卡片，响应式布局)                          │
└─────────────────────────────────────────────────────────────┘
┌──────────────────────────────┬──────────────────────────────┐
│  Token 使用趋势（曲线图）     │  模型使用分布（饼图）         │
│  - 24小时数据                 │  - Top 10 模型               │
│  - 3条曲线：请求数/输入/输出  │  - 显示请求数和占比          │
└──────────────────────────────┴──────────────────────────────┘
┌─────────────────────────────────────────────────────────────┐
│  最近使用 TOP 10（多条曲线图）                                │
│  - 显示前10个API Key的24小时使用趋势                          │
│  - 每个API Key一条曲线                                        │
└─────────────────────────────────────────────────────────────┘
```

---

### 2.3 数据流设计

#### 前端数据模型（DashboardViewModel）
```typescript
interface DashboardViewModel {
  // 指标数据
  metrics: UsageMetricsOutputDto;

  // Token使用趋势（24小时）
  tokenTrend: UsageTrendOutputDto[];

  // 模型使用分布（Top 10）
  modelDistribution: ModelDistributionOutputDto[];

  // API Key使用趋势（Top 10）
  apiKeyTrend: ApiKeyTrendOutputDto[];

  // 账户指标
  accounts: AccountTokenMetricsOutputDto;

  // 订阅指标
  subscriptions: SubscriptionMetricsOutputDto;
}
```

#### 后端 API 端点（Dashboard使用）
```
✅ GET /api/v1/usage/query/metrics              → UsageMetricsOutputDto
✅ GET /api/v1/usage/query/trend                → List<UsageTrendOutputDto>
✅ GET /api/v1/usage/query/top-api-keys         → List<ApiKeyTrendOutputDto>
✅ GET /api/v1/usage/query/model-distribution   → List<ModelDistributionOutputDto>
✅ GET /api/v1/account-tokens/metrics           → AccountTokenMetricsOutputDto
✅ GET /api/v1/api-keys/metrics                 → SubscriptionMetricsOutputDto

⚠️ 保留但Dashboard不使用：GET /api/v1/usage/query/logs
```

---

## 三、文件变更清单

### 3.1 前端文件变更

#### 📝 需要修改的文件

```
frontend/src/app/features/platform/
├── components/dashboard/
│   ├── dashboard.ts                          # 修改：调整数据获取逻辑，移除logs相关
│   ├── dashboard.html                        # 修改：调整布局，移除RequestLogTable，新增图表
│   └── widgets/
│       ├── metrics-cards/
│       │   ├── metrics-cards.ts              # 修改：调整第2个卡片为"今日消耗"
│       │   └── metrics-cards.html            # 修改：调整卡片展示内容
│       ├── usage-trend-chart/
│       │   ├── usage-trend-chart.ts          # 修改：支持3条曲线（请求/输入/输出Token）
│       │   └── usage-trend-chart.html        # 修改：调整图表配置
│       ├── model-distribution-chart/         # 🆕 新增：模型分布图表组件
│       │   ├── model-distribution-chart.ts
│       │   ├── model-distribution-chart.html
│       │   └── model-distribution-chart.css
│       └── api-key-trend-chart/              # 🆕 新增：API Key趋势图表组件
│           ├── api-key-trend-chart.ts
│           ├── api-key-trend-chart.html
│           └── api-key-trend-chart.css
├── services/
│   ├── dashboard-service.ts                  # 修改：调整forkJoin，移除getLogs，新增getModelDistribution和getTopApiKeys
│   └── usage-query.service.ts                # 修改：移除getLogs方法，新增getModelDistribution和getTopApiKeys
└── models/
    ├── dashboard.dto.ts                      # 修改：移除logs字段，新增modelDistribution和apiKeyTrend
    └── usage.dto.ts                          # 修改：移除UsageLogOutputDto导入（如果仅Dashboard使用）
```

#### ❌ 需要删除的文件

```
frontend/src/app/features/platform/
└── components/dashboard/
    └── widgets/
        └── request-log-table/                # 🗑️ 删除整个目录
            ├── request-log-table.ts
            ├── request-log-table.html
            └── request-log-table.css
```

---

### 3.2 后端文件变更

#### ✅ 确认已存在的接口（无需修改）

```
backend/src/AiRelay.Api/Controllers/
└── UsageQueryController.cs
    ├── GET /metrics                          # ✅ 已存在
    ├── GET /trend                            # ✅ 已存在
    ├── GET /top-api-keys                     # ✅ 需确认
    ├── GET /model-distribution               # ✅ 需确认
    └── GET /logs                             # ⚠️ 保留（后续其他页面使用）

backend/src/AiRelay.Application/UsageRecords/Dtos/Query/
├── UsageMetricsOutputDto.cs                  # ✅ 确认包含Token和Cost字段
├── UsageTrendOutputDto.cs                    # ✅ 确认包含InputTokens和OutputTokens
├── ApiKeyTrendOutputDto.cs                   # ✅ 需确认
├── ModelDistributionOutputDto.cs             # ✅ 需确认
└── UsageLogOutputDto.cs                      # ⚠️ 保留（后续其他页面使用）
```

---

### 3.3 Mock 文件变更

#### 📝 需要修改的文件

```
frontend/_mock/
├── api/
│   └── usage-query.ts                        # 修改：移除logs端点，新增modelDistribution和topApiKeys
└── data/
    └── usage-query-data.ts                   # 🆕 新增：模型分布和API Key趋势的Mock数据
```

#### ❌ 需要删除的内容

```
frontend/_mock/api/usage-query.ts
└── GET /api/v1/usage/query/logs              # 🗑️ 删除此Mock端点（Dashboard不再使用）
```

---

## 四、组件设计详情

### 4.1 MetricsCards 组件

#### 输入数据
```typescript
@Input() metrics: UsageMetricsOutputDto;
@Input() accounts: AccountTokenMetricsOutputDto;
@Input() subscriptions: SubscriptionMetricsOutputDto;
```

#### 卡片2：今日消耗（新设计）
```typescript
// 主指标
totalCost: number;              // metrics.totalCost

// 副指标
inputTokens: number;            // metrics.totalInputTokens
outputTokens: number;           // metrics.totalOutputTokens

// 趋势（需要后端提供或前端计算）
costTrend: number;              // 相比昨天的百分比变化

// 展示格式
主标题: "今日消耗"
主数值: "¥X.XXXX"
副标题: "输入 XXX / 输出 XXX"
趋势: "↑ 12.3%" (红色) 或 "↓ 5.2%" (绿色)
```

---

### 4.2 UsageTrendChart 组件（Token使用趋势）

#### 输入数据
```typescript
@Input() trendData: UsageTrendOutputDto[];

interface UsageTrendOutputDto {
  time: string;           // "HH:mm"
  requests: number;       // 请求数
  inputTokens: number;    // 输入Token
  outputTokens: number;   // 输出Token
}
```

#### 图表配置
- **类型**: 多条曲线图（Line Chart）
- **X轴**: 时间（24小时，每小时一个点）
- **Y轴**: 数量（自适应刻度）
- **曲线**:
  1. 请求数（蓝色，主Y轴）
  2. 输入Token（绿色，次Y轴）
  3. 输出Token（橙色，次Y轴）
- **PrimeNG组件**: `<p-chart type="line">`
- **响应式**: 占据左侧50%宽度（桌面），100%宽度（移动端）

---

### 4.3 ModelDistributionChart 组件（模型使用分布）

#### 输入数据
```typescript
@Input() distributionData: ModelDistributionOutputDto[];

interface ModelDistributionOutputDto {
  model: string;          // 模型名称
  requestCount: number;   // 请求数
  totalTokens: number;    // 总Token数
  totalCost: number;      // 总成本
  percentage: number;     // 占比百分比
}
```

#### 图表配置
- **类型**: 饼图（Pie Chart）
- **数据**: Top 10 模型
- **显示**: 模型名称 + 请求数 + 占比
- **颜色**: 使用PrimeNG主题色板
- **PrimeNG组件**: `<p-chart type="pie">`
- **响应式**: 占据右侧50%宽度（桌面），100%宽度（移动端）

---

### 4.4 ApiKeyTrendChart 组件（最近使用 TOP 10）

#### 输入数据
```typescript
@Input() apiKeyTrendData: ApiKeyTrendOutputDto[];

interface ApiKeyTrendOutputDto {
  apiKeyName: string;                 // API Key名称
  trend: UsageTrendOutputDto[];       // 24小时趋势数据
  totalRequests: number;              // 总请求数
}
```

#### 图表配置
- **类型**: 多条曲线图（Line Chart）
- **X轴**: 时间（24小时）
- **Y轴**: 请求数
- **曲线**: 每个API Key一条曲线（最多10条）
- **图例**: 显示API Key名称和总请求数
- **颜色**: 自动分配10种不同颜色
- **PrimeNG组件**: `<p-chart type="line">`
- **响应式**: 占据100%宽度

---

## 五、实施步骤

### 阶段1：后端接口确认
1. ✅ 确认 `UsageMetricsOutputDto` 包含 `TotalInputTokens`、`TotalOutputTokens`、`TotalCost`
2. ✅ 确认 `UsageTrendOutputDto` 包含 `InputTokens`、`OutputTokens`
3. ✅ 确认 `ApiKeyTrendOutputDto` 是否存在
4. ✅ 确认 `ModelDistributionOutputDto` 是否存在
5. ✅ 确认 `/api/v1/usage/query/top-api-keys` 端点是否存在
6. ✅ 确认 `/api/v1/usage/query/model-distribution` 端点是否存在

### 阶段2：Mock 数据调整
1. ❌ 删除 `_mock/api/usage-query.ts` 中的 `logs` 端点
2. ✅ 新增 `modelDistribution` Mock 端点
3. ✅ 新增 `topApiKeys` Mock 端点
4. ✅ 创建 `_mock/data/usage-query-data.ts` 包含模拟数据

### 阶段3：前端组件开发
1. 🆕 创建 `ModelDistributionChart` 组件
2. 🆕 创建 `ApiKeyTrendChart` 组件
3. 📝 修改 `MetricsCards` 组件（调整第2个卡片）
4. 📝 修改 `UsageTrendChart` 组件（支持3条曲线）
5. ❌ 删除 `RequestLogTable` 组件

### 阶段4：前端服务和数据流
1. 📝 修改 `UsageQueryService`（新增方法，删除getLogs）
2. 📝 修改 `DashboardService`（调整forkJoin）
3. 📝 修改 `dashboard.dto.ts`（调整ViewModel）
4. 📝 修改 `usage.dto.ts`（移除UsageLogOutputDto导入）

### 阶段5：前端页面集成
1. 📝 修改 `dashboard.ts`（调整数据获取和状态管理）
2. 📝 修改 `dashboard.html`（调整布局和组件引用）
3. ✅ 测试响应式布局
4. ✅ 测试数据刷新机制

### 阶段6：测试和优化
1. ✅ 功能测试（所有图表正常显示）
2. ✅ 数据测试（Mock数据正确）
3. ✅ 响应式测试（移动端/桌面端）
4. ✅ 性能测试（轮询不影响性能）
5. ✅ 主题测试（亮色/暗色模式）

---

## 六、技术要点

### 6.1 PrimeNG 组件使用

#### Chart 组件（p-chart）
```typescript
import { ChartModule } from 'primeng/chart';

// 在组件中
imports: [ChartModule]

// 模板中
<p-chart type="line" [data]="chartData" [options]="chartOptions" />
```

#### Card 组件（p-card）
```typescript
import { CardModule } from 'primeng/card';

// 在组件中
imports: [CardModule]

// 模板中
<p-card>
  <ng-template pTemplate="header">标题</ng-template>
  <ng-template pTemplate="content">内容</ng-template>
</p-card>
```

### 6.2 响应式布局（Tailwind CSS）

```html
<!-- 2列布局（桌面），1列布局（移动端） -->
<div class="grid grid-cols-1 lg:grid-cols-2 gap-4">
  <div>左侧内容</div>
  <div>右侧内容</div>
</div>

<!-- 4列布局（桌面），2列（平板），1列（移动端） -->
<div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
  <div>卡片1</div>
  <div>卡片2</div>
  <div>卡片3</div>
  <div>卡片4</div>
</div>
```

### 6.3 Angular Signals 使用

```typescript
// 定义Signal
dashboardData = signal<DashboardViewModel | null>(null);
isRefreshing = signal<boolean>(false);

// 更新Signal
this.dashboardData.set(data);
this.isRefreshing.set(true);

// 在模板中使用
@if (dashboardData(); as data) {
  <app-metrics-cards [metrics]="data.metrics" />
}
```

### 6.4 Chart.js 配置（通过PrimeNG）

```typescript
// 多条曲线配置（双Y轴）
chartData = {
  labels: ['00:00', '01:00', ...],
  datasets: [
    {
      label: '请求数',
      data: [100, 200, ...],
      borderColor: '#3B82F6',
      backgroundColor: 'rgba(59, 130, 246, 0.1)',
      tension: 0.4,
      yAxisID: 'y'
    },
    {
      label: '输入Token',
      data: [1000, 2000, ...],
      borderColor: '#10B981',
      backgroundColor: 'rgba(16, 185, 129, 0.1)',
      tension: 0.4,
      yAxisID: 'y1'
    },
    {
      label: '输出Token',
      data: [500, 1000, ...],
      borderColor: '#F59E0B',
      backgroundColor: 'rgba(245, 158, 11, 0.1)',
      tension: 0.4,
      yAxisID: 'y1'
    }
  ]
};

chartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  interaction: {
    mode: 'index',
    intersect: false
  },
  plugins: {
    legend: { position: 'top' },
    tooltip: { mode: 'index', intersect: false }
  },
  scales: {
    x: { grid: { display: false } },
    y: {
      type: 'linear',
      display: true,
      position: 'left',
      title: { display: true, text: '请求数' }
    },
    y1: {
      type: 'linear',
      display: true,
      position: 'right',
      title: { display: true, text: 'Token数' },
      grid: { drawOnChartArea: false }
    }
  }
};
```

---

## 七、文件树形结构（完整）

### 7.1 前端文件结构

```
frontend/src/app/features/platform/
├── components/dashboard/
│   ├── dashboard.ts                          # 📝 修改
│   ├── dashboard.html                        # 📝 修改
│   ├── dashboard.css                         # ✅ 保持
│   └── widgets/
│       ├── metrics-cards/
│       │   ├── metrics-cards.ts              # 📝 修改（调整第2个卡片）
│       │   ├── metrics-cards.html            # 📝 修改
│       │   └── metrics-cards.css             # ✅ 保持
│       ├── usage-trend-chart/
│       │   ├── usage-trend-chart.ts          # 📝 修改（支持3条曲线）
│       │   ├── usage-trend-chart.html        # 📝 修改
│       │   └── usage-trend-chart.css         # ✅ 保持
│       ├── model-distribution-chart/         # 🆕 新增
│       │   ├── model-distribution-chart.ts
│       │   ├── model-distribution-chart.html
│       │   └── model-distribution-chart.css
│       ├── api-key-trend-chart/              # 🆕 新增
│       │   ├── api-key-trend-chart.ts
│       │   ├── api-key-trend-chart.html
│       │   └── api-key-trend-chart.css
│       └── request-log-table/                # 🗑️ 删除整个目录
├── services/
│   ├── dashboard-service.ts                  # 📝 修改
│   └── usage-query.service.ts                # 📝 修改
└── models/
    ├── dashboard.dto.ts                      # 📝 修改
    └── usage.dto.ts                          # 📝 修改（移除UsageLogOutputDto导入）
```

### 7.2 后端文件结构（仅确认，不修改）

```
backend/src/AiRelay.Api/Controllers/
└── UsageQueryController.cs                   # ✅ 确认接口存在

backend/src/AiRelay.Application/UsageRecords/
├── AppServices/
│   ├── IUsageQueryAppService.cs              # ✅ 确认接口存在
│   └── UsageQueryAppService.cs               # ✅ 确认接口存在
└── Dtos/Query/
    ├── UsageMetricsOutputDto.cs              # ✅ 确认字段完整
    ├── UsageTrendOutputDto.cs                # ✅ 确认字段完整
    ├── ApiKeyTrendOutputDto.cs               # ✅ 确认存在
    ├── ModelDistributionOutputDto.cs         # ✅ 确认存在
    └── UsageLogOutputDto.cs                  # ⚠️ 保留
```

### 7.3 Mock 文件结构

```
frontend/_mock/
├── api/
│   └── usage-query.ts                        # 📝 修改
│       ├── GET /api/v1/usage/query/metrics              # ✅ 保持
│       ├── GET /api/v1/usage/query/trend                # ✅ 保持
│       ├── GET /api/v1/usage/query/top-api-keys         # 🆕 新增
│       ├── GET /api/v1/usage/query/model-distribution   # 🆕 新增
│       └── GET /api/v1/usage/query/logs                 # 🗑️ 删除
└── data/
    └── usage-query-data.ts                   # 🆕 新增（包含所有Mock数据）
```

---

## 八、数据契约（DTO定义）

### 8.1 UsageMetricsOutputDto（后端已存在，需确认字段）

```csharp
public class UsageMetricsOutputDto
{
    public int TotalRequests { get; set; }
    public decimal RequestsTrend { get; set; }
    public decimal CurrentRps { get; set; }
    public long TotalInputTokens { get; set; }      // ✅ 需要
    public long TotalOutputTokens { get; set; }     // ✅ 需要
    public decimal TotalCost { get; set; }          // ✅ 需要
    public int SuccessRequests { get; set; }
    public int FailedRequests { get; set; }
}
```

### 8.2 UsageTrendOutputDto（后端已存在，需确认字段）

```csharp
public class UsageTrendOutputDto
{
    public string Time { get; set; }            // "HH:mm"
    public int Requests { get; set; }
    public long InputTokens { get; set; }       // ✅ 需要
    public long OutputTokens { get; set; }      // ✅ 需要
}
```

### 8.3 ApiKeyTrendOutputDto（后端已存在，需确认）

```csharp
public class ApiKeyTrendOutputDto
{
    public string ApiKeyName { get; set; }
    public List<UsageTrendOutputDto> Trend { get; set; }
    public int TotalRequests { get; set; }
}
```

### 8.4 ModelDistributionOutputDto（后端已存在，需确认）

```csharp
public class ModelDistributionOutputDto
{
    public string Model { get; set; }
    public int RequestCount { get; set; }
    public long TotalTokens { get; set; }
    public decimal TotalCost { get; set; }
    public decimal Percentage { get; set; }
}
```

---

## 九、验收标准

### 9.1 功能验收
- ✅ Metric Cards显示正确（4个卡片，第2个为"今日消耗"）
- ✅ Token使用趋势显示3条曲线（请求/输入/输出）
- ✅ 模型使用分布显示Top 10模型
- ✅ 最近使用TOP 10显示10条API Key曲线
- ✅ 实时请求日志已从Dashboard移除

### 9.2 UI验收
- ✅ 使用PrimeNG默认风格
- ✅ 响应式布局正常（移动端/桌面端）
- ✅ 亮色/暗色主题切换正常
- ✅ 图表交互正常（hover、legend点击）

### 9.3 代码验收
- ✅ 遵循前端开发规范
- ✅ 使用Angular Signals管理状态
- ✅ 使用inject()进行依赖注入
- ✅ 组件使用OnPush变更检测
- ✅ 无TypeScript类型错误
- ✅ 无ESLint警告

### 9.4 性能验收
- ✅ 首次加载时间 < 2秒
- ✅ 轮询刷新不卡顿
- ✅ 图表渲染流畅（60fps）

---

## 十、附录

### 10.1 相关文档
- [PrimeNG Chart 文档](https://primeng.org/chart)
- [Chart.js 文档](https://www.chartjs.org/docs/latest/)
- [Angular Signals 文档](https://angular.io/guide/signals)
- [前端开发规范](./docs/standards/code_standard/frontend_develop.md)

### 10.2 参考资料
- 现有Dashboard实现：`frontend/src/app/features/platform/components/dashboard/`
- 现有后端接口：`backend/src/AiRelay.Api/Controllers/UsageQueryController.cs`
- 现有Mock数据：`frontend/_mock/api/usage-query.ts`

---

**方案制定日期**: 2026-02-05
**方案版本**: v1.0
**待审核**: ✅

**重要说明**:
- ⚠️ 后端实时请求日志接口（`GET /api/v1/usage/query/logs`）保留不删除
- ⚠️ 后端 `UsageLogOutputDto` 保留不删除
- ✅ 仅在前端Dashboard中移除实时请求日志功能
