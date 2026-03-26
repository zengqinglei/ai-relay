# UsageRecord 子表重构设计方案

> 版本：v1.1 | 日期：2026-03-26 | 状态：待审查

---

## 一、背景

### 1.1 现状

`SmartReverseProxyMiddleware` 在处理一次客户端请求时，存在两层重试循环：

- **外层**：账号切换（最多 10 次）
- **内层**：同账号重试（最多 3 次）

当前每次内层循环迭代均产生一条独立的 `UsageRecord`，通过 `CorrelationId` 关联。写入路径为：

```
内层循环开始 → TryEnqueue(UsageRecordStartItem)
内层循环结束 → TryEnqueue(UsageRecordEndItem)   ← finally 块
```

### 1.2 问题

| 问题 | 描述 |
|------|------|
| 职责混淆 | 一条 `UsageRecord` 既代表"客户端请求"又代表"一次上游尝试"，语义不清 |
| 统计失真 | 客户端成功率统计需过滤中间失败记录，无原生支持 |
| 账号诊断受限 | 无法直接查询"某账号参与了哪些请求的哪次尝试" |
| 重试链路不可见 | 前端详情页只展示单次上游信息，无法呈现完整重试过程 |
| 写入路径复杂 | Start/End 分段入队，消费端需两次 DB 操作（Insert + Update）拼接一条记录 |

### 1.3 目标

1. `UsageRecord` 语义收敛为**客户端请求维度**（1次客户端请求 = 1条记录）
2. `UsageRecordAttempt` 承载**每次上游尝试**的账号、状态、耗时等信息
3. 保留三段式写入路径（Start → AttemptItem × N → End），长耗时请求在使用记录页实时可见 `InProgress` 状态
4. 前端详情页新增「尝试记录」Tab，用户可感知重试过程

---

## 二、核心策略

### 2.1 数据模型设计

#### 实体职责划分

```
UsageRecord                    ← 客户端请求维度（1条/请求）
  ├── UsageRecordDetail        ← 下游大字段（1:1，下游 Headers/Body/Response）
  └── UsageRecordAttempt[]     ← 上游尝试（1:N，每次尝试一条）
        └── UsageRecordAttemptDetail  ← 上游大字段（1:1，上游 Headers/Body/Response）
```

#### UsageRecord 字段调整

| 字段 | 变化 | 说明 |
|------|------|------|
| `Platform` | 保留 | 由路由元数据决定，请求级固定 |
| `ProviderGroupId/Name` | 保留 | 由 ApiKey 绑定决定，请求级固定 |
| `DownModelId/ClientIp/UserAgent` | 保留 | 客户端请求信息 |
| `DurationMs` | 保留 | 整体耗时（首次尝试开始 → 最终响应） |
| `InputTokens/OutputTokens/Cost` | 保留 | 最终成功尝试的 Token 统计 |
| `AttemptCount` | **新增** | 总尝试次数（End 时写入） |
| `AccountTokenId/Name` | **移除** | 迁移至 `UsageRecordAttempt` |
| `UpModelId/UserAgent/RequestUrl` | **移除** | 迁移至 `UsageRecordAttempt` |
| `UpStatusCode` | **移除** | 迁移至 `UsageRecordAttempt` |

#### UsageRecordDetail 字段调整

| 字段 | 变化 | 说明 |
|------|------|------|
| `DownRequestHeaders/Body/ResponseBody` | 保留 | 下游大字段 |
| `UpRequestHeaders/Body/ResponseBody` | **移除** | 迁移至 `UsageRecordAttemptDetail` |

### 2.2 调整文件树形目录

```
backend/src/
├── AiRelay.Domain/
│   └── UsageRecords/
│       └── Entities/
│           ├── UsageRecord.cs                    ← 重构：移除账号字段，新增 AttemptCount + Attempts 导航
│           ├── UsageRecordDetail.cs              ← 调整：移除 Up* 字段，仅保留 Down* 字段
│           ├── UsageRecordAttempt.cs             ← 新增：每次上游尝试实体
│           └── UsageRecordAttemptDetail.cs       ← 新增：上游大字段（Headers/Body/Response）
│
├── AiRelay.Application/
│   └── UsageRecords/
│       ├── AppServices/
│       │   ├── IUsageLifecycleAppService.cs      ← 调整：新增 AddAttemptAsync
│       │   └── UsageLifecycleAppService.cs       ← 重构：AddAttempt 写入子记录
│       ├── Dtos/
│       │   ├── Lifecycle/
│       │   │   ├── StartUsageInputDto.cs         ← 调整：移除账号/Up 字段（仅保留客户端级信息）
│       │   │   ├── AddAttemptInputDto.cs         ← 新增：单次尝试数据 DTO
│       │   │   └── FinishUsageInputDto.cs        ← 调整：新增 AttemptCount、DownResponseBody
│       │   └── Query/
│       │       ├── UsageRecordOutputDto.cs       ← 调整：新增 AttemptCount，移除 AccountTokenName/UpStatusCode/UpModelId
│       │       ├── UsageRecordDetailDto.cs       ← 调整：新增 Attempts 列表，移除 Up* 字段
│       │       └── UsageRecordAttemptOutputDto.cs ← 新增
│       └── Mappings/
│           └── UsageRecordProfile.cs             ← 调整：新增 Attempt 映射
│
├── AiRelay.Infrastructure/
│   └── Persistence/
│       ├── EntityConfigurations/
│       │   └── UsageRecordEntityConfiguration.cs ← 调整：新增 Attempt 表配置和索引
│       └── Migrations/
│           └── {timestamp}_AddUsageRecordAttempt.cs ← 新增迁移
│
└── AiRelay.Api/
    ├── HostedServices/Workers/
    │   └── AccountUsageRecordHostedService.cs    ← 调整：新增 UsageRecordAttemptItem，switch 新增分支
    └── Middleware/SmartProxy/
        └── SmartReverseProxyMiddleware.cs        ← 重构：Start 提升到外层，内层 finally 入队 AttemptItem

frontend/src/app/features/platform/
├── models/
│   └── usage.dto.ts                              ← 调整：新增 AttemptCount、UsageRecordAttemptOutputDto
└── components/usage-records/
    ├── usage-records.html                        ← 调整：状态列新增重试 badge
    └── usage-record-detail-dialog.ts             ← 调整：新增「尝试记录」Tab
```

### 2.3 核心代码

#### 2.3.1 新实体：UsageRecordAttempt.cs

```csharp
public class UsageRecordAttempt : Entity<Guid>
{
    public Guid UsageRecordId { get; private set; }
    public int AttemptNumber { get; private set; }
    public Guid AccountTokenId { get; private set; }
    public string AccountTokenName { get; private set; }
    public ProviderPlatform Platform { get; private set; }
    public string? UpModelId { get; private set; }
    public string? UpUserAgent { get; private set; }
    public string? UpRequestUrl { get; private set; }
    public int? UpStatusCode { get; private set; }
    public long DurationMs { get; private set; }
    public UsageStatus Status { get; private set; }
    public string? StatusDescription { get; private set; }

    public UsageRecordAttemptDetail Detail { get; private set; } = null!;

    public UsageRecordAttempt(
        Guid usageRecordId, int attemptNumber,
        Guid accountTokenId, string accountTokenName, ProviderPlatform platform,
        string? upModelId, string? upUserAgent, string? upRequestUrl,
        string? upRequestHeaders, string? upRequestBody, string? upResponseBody,
        int? upStatusCode, long durationMs,
        UsageStatus status, string? statusDescription)
    {
        Id = Guid.CreateVersion7();
        UsageRecordId = usageRecordId;
        AttemptNumber = attemptNumber;
        AccountTokenId = accountTokenId;
        AccountTokenName = accountTokenName;
        Platform = platform;
        UpModelId = upModelId;
        UpUserAgent = upUserAgent;
        UpRequestUrl = upRequestUrl;
        UpStatusCode = upStatusCode;
        DurationMs = durationMs;
        Status = status;
        StatusDescription = statusDescription?.Length > 2048
            ? statusDescription[..2045] + "..." : statusDescription;
        Detail = new UsageRecordAttemptDetail(Id, upRequestHeaders, upRequestBody, upResponseBody);
    }

    private UsageRecordAttempt() { AccountTokenName = null!; }
}
```

#### 2.3.2 UsageRecord.cs 关键变化

```csharp
// 新增字段
public int AttemptCount { get; private set; }

// 新增导航属性
private readonly List<UsageRecordAttempt> _attempts = [];
public IReadOnlyList<UsageRecordAttempt> Attempts => _attempts.AsReadOnly();

// 构造函数移除：AccountTokenId/Name、UpModelId/UserAgent/RequestUrl
// Complete() 新增 attemptCount 参数，移除 upStatusCode
public void Complete(
    long duration, UsageStatus status, string? statusDescription,
    string? downResponseBody,
    int? inputTokens, int? outputTokens,
    int? cacheReadTokens, int? cacheCreationTokens,
    decimal? baseCost, int attemptCount)
{
    DurationMs = duration;
    Status = status;
    StatusDescription = statusDescription?.Length > 2048
        ? statusDescription[..2045] + "..." : statusDescription;
    Detail.Complete(downResponseBody);
    InputTokens = inputTokens;
    OutputTokens = outputTokens;
    CacheReadTokens = cacheReadTokens;
    CacheCreationTokens = cacheCreationTokens;
    AttemptCount = attemptCount;
    if (baseCost.HasValue)
    {
        BaseCost = baseCost;
        FinalCost = BaseCost.Value * GroupRateMultiplier;
    }
}
```

#### 2.3.3 写入 DTO 调整

**StartUsageInputDto.cs（调整：移除账号/Up 字段）**

```csharp
public record StartUsageInputDto(
    Guid UsageRecordId,
    string CorrelationId,
    ProviderPlatform Platform,
    Guid ApiKeyId,
    string ApiKeyName,
    Guid ProviderGroupId,
    string ProviderGroupName,
    decimal GroupRateMultiplier,
    bool IsStreaming,
    string DownRequestMethod,
    string DownRequestUrl,
    string? DownModelId,
    string? DownClientIp,
    string? DownUserAgent,
    string? DownRequestHeaders,
    string? DownRequestBody
    // 移除：AccountTokenId/Name、UpModelId/UserAgent/RequestUrl/Headers/Body/ResponseBody
);
```

**AddAttemptInputDto.cs（新增）**

```csharp
public record AddAttemptInputDto(
    Guid UsageRecordId,
    int AttemptNumber,
    Guid AccountTokenId,
    string AccountTokenName,
    ProviderPlatform Platform,
    string? UpModelId,
    string? UpUserAgent,
    string? UpRequestUrl,
    string? UpRequestHeaders,
    string? UpRequestBody,
    string? UpResponseBody,
    int? UpStatusCode,
    long DurationMs,
    UsageStatus Status,
    string? StatusDescription
);
```

**FinishUsageInputDto.cs（调整：新增 AttemptCount、DownResponseBody）**

```csharp
public record FinishUsageInputDto(
    Guid UsageRecordId,
    long TotalDurationMs,
    UsageStatus FinalStatus,
    string? FinalStatusDescription,
    string? DownResponseBody,          // 新增
    int? InputTokens,
    int? OutputTokens,
    int? CacheReadTokens,
    int? CacheCreationTokens,
    decimal? BaseCost,
    int AttemptCount                   // 新增
);
```

#### 2.3.4 AccountUsageRecordHostedService.cs 关键变化

```csharp
// 新增 UsageRecordAttemptItem（保留 StartItem / EndItem）
public record UsageRecordAttemptItem(AddAttemptInputDto Data) : IUsageRecordItem
{
    public Guid UsageRecordId => Data.UsageRecordId;
}

// ExecuteAsync switch 新增分支：
case UsageRecordAttemptItem attemptItem:
    await usageLifecycleAppService.AddAttemptAsync(attemptItem.Data, stoppingToken);
    break;
```

#### 2.3.5 SmartReverseProxyMiddleware.cs 关键变化

三段式写入路径：

```
请求开始 → TryEnqueue(UsageRecordStartItem)      ← INSERT UsageRecord, Status=InProgress
内层 finally → TryEnqueue(UsageRecordAttemptItem) ← INSERT UsageRecordAttempt（每次尝试一条）
外层 finally → TryEnqueue(UsageRecordEndItem)     ← UPDATE UsageRecord，写入最终状态 + AttemptCount
```

```csharp
public async Task InvokeAsync(HttpContext context)
{
    var (platform, apiKeyId, apiKeyName) = ValidateAndGetContext(context);
    var correlationId = correlationIdProvider.Get() ?? correlationIdProvider.Create();
    var chatModelHandler = chatModelHandlerFactory.CreateHandler(platform);
    var downContext = await downstreamRequestProcessor.ProcessAsync(context, chatModelHandler, apiKeyId, context.RequestAborted);

    // 提升到外层：整体请求级变量
    var usageRecordId = Guid.CreateVersion7();
    var attemptNumber = 0;
    var overallStopwatch = Stopwatch.StartNew();
    UsageStatus finalStatus = UsageStatus.Failed;
    string? finalStatusDescription = null;
    StreamForwardResult? finalForwardResult = null;
    string? downResponseBody = null;
    Guid lastProviderGroupId = Guid.Empty;
    string lastProviderGroupName = string.Empty;
    decimal lastGroupRateMultiplier = 1m;

    var downRequestBody = _loggingOptions.IsBodyLoggingEnabled
        ? (downContext.IsMultipart
            ? "[Multipart Data - Logging Skipped]"
            : downContext.GetBodyPreview(_loggingOptions.MaxBodyLength))
        : null;

    // 提前入队 StartItem（UsageRecord InProgress 立即可见）
    usageRecordHostedService.TryEnqueue(new UsageRecordStartItem(new StartUsageInputDto(
        UsageRecordId: usageRecordId,
        CorrelationId: correlationId,
        Platform: platform,
        ApiKeyId: apiKeyId,
        ApiKeyName: apiKeyName,
        ProviderGroupId: Guid.Empty,          // 尚未选号，由消费端容错处理或于 End 更新
        ProviderGroupName: string.Empty,
        GroupRateMultiplier: 1m,
        IsStreaming: downContext.IsStreaming,
        DownRequestMethod: context.Request.Method,
        DownRequestUrl: context.Request.GetDisplayUrl(),
        DownModelId: downContext.ModelId,
        DownClientIp: context.Connection.RemoteIpAddress?.ToString(),
        DownUserAgent: context.Request.Headers.UserAgent.ToString(),
        DownRequestHeaders: _loggingOptions.IsBodyLoggingEnabled
            ? CaptureHeaders(downContext.Headers) : null,
        DownRequestBody: downRequestBody
    )));

    try
    {
        var excludedAccountIds = new HashSet<Guid>();
        var accountSwitchCount = 0;

        while (true)
        {
            if (accountSwitchCount >= MaxAccountSwitches)
                throw new BadRequestException($"已尝试 {MaxAccountSwitches} 个账号，均不可用");

            var selectResult = await smartProxyAppService.SelectAccountAsync(...);
            lastProviderGroupId = selectResult.ProviderGroupId;
            lastProviderGroupName = selectResult.ProviderGroupName;
            lastGroupRateMultiplier = selectResult.GroupRateMultiplier;

            // ...指纹逻辑不变...

            var shouldSwitchAccount = false;
            var degradationLevel = 0;

            while (!shouldSwitchAccount)
            {
                var attemptStopwatch = Stopwatch.StartNew();
                attemptNumber++;
                int? httpStatusCode = null;
                var attemptStatus = UsageStatus.Failed;
                string? attemptStatusDesc = null;
                string? upResponseBody = null;
                UpRequestContext? upContext = null;

                try
                {
                    var accountedHandler = chatModelHandlerFactory.CreateHandler(...);
                    upContext = await accountedHandler.ProcessRequestContextAsync(downContext, degradationLevel, context.RequestAborted);

                    using var response = await accountedHandler.ProxyRequestAsync(upContext, context.RequestAborted);
                    httpStatusCode = (int)response.StatusCode;

                    if (response.IsSuccessStatusCode)
                    {
                        attemptStatus = UsageStatus.Success;
                        finalStatus = UsageStatus.Success;
                        // ...成功处理逻辑不变...
                        finalForwardResult = forwardResult;
                        downResponseBody = forwardResult?.CapturedBody;
                        return;
                    }
                    else
                    {
                        attemptStatus = UsageStatus.Failed;
                        // ...错误分析、决策逻辑不变...
                        attemptStatusDesc = usageStatusDescription;
                    }
                }
                finally
                {
                    attemptStopwatch.Stop();
                    await concurrencyStrategy.ReleaseSlotAsync(selectResult.AccountToken.Id, activeRequestId);

                    // 每次尝试结束后入队 AttemptItem
                    usageRecordHostedService.TryEnqueue(new UsageRecordAttemptItem(new AddAttemptInputDto(
                        UsageRecordId: usageRecordId,
                        AttemptNumber: attemptNumber,
                        AccountTokenId: selectResult.AccountToken.Id,
                        AccountTokenName: selectResult.AccountToken.Name,
                        Platform: platform,
                        UpModelId: upContext?.MappedModelId,
                        UpUserAgent: upContext?.GetUserAgent(),
                        UpRequestUrl: upContext?.GetFullUrl(),
                        UpRequestHeaders: _loggingOptions.IsBodyLoggingEnabled
                            ? CaptureHeaders(upContext?.Headers) : null,
                        UpRequestBody: _loggingOptions.IsBodyLoggingEnabled
                            ? (upContext?.BodyJson?.ToString()) : null,
                        UpResponseBody: _loggingOptions.IsBodyLoggingEnabled
                            ? upResponseBody : null,
                        UpStatusCode: httpStatusCode,
                        DurationMs: attemptStopwatch.ElapsedMilliseconds,
                        Status: attemptStatus,
                        StatusDescription: attemptStatusDesc
                    )));
                }
            }

            if (shouldSwitchAccount)
            {
                excludedAccountIds.Add(selectResult.AccountToken.Id);
                accountSwitchCount++;
            }
        }
    }
    catch (Exception ex)
    {
        finalStatus = UsageStatus.Failed;
        finalStatusDescription = ex.Message;
        // ...现有错误格式化逻辑不变...
    }
    finally
    {
        overallStopwatch.Stop();

        // 外层 finally 入队 EndItem（更新主记录为最终状态）
        usageRecordHostedService.TryEnqueue(new UsageRecordEndItem(new FinishUsageInputDto(
            UsageRecordId: usageRecordId,
            TotalDurationMs: overallStopwatch.ElapsedMilliseconds,
            FinalStatus: finalStatus,
            FinalStatusDescription: finalStatusDescription,
            DownResponseBody: _loggingOptions.IsBodyLoggingEnabled ? downResponseBody : null,
            InputTokens: finalForwardResult?.Usage?.InputTokens,
            OutputTokens: finalForwardResult?.Usage?.OutputTokens,
            CacheReadTokens: finalForwardResult?.Usage?.CacheReadTokens,
            CacheCreationTokens: finalForwardResult?.Usage?.CacheCreationTokens,
            BaseCost: null, // 由 DomainService 根据 Token 计算
            AttemptCount: attemptNumber
        )));
    }
}
```

#### 2.3.6 前端 DTO 调整：usage.dto.ts

```typescript
export interface UsageRecordOutputDto {
  // ...现有字段保持不变...
  attemptCount: number;  // 新增
  // 移除：accountTokenName、upStatusCode、upModelId（迁移至 Attempt）
}

export interface UsageRecordAttemptOutputDto {
  attemptNumber: number;
  accountTokenName: string;
  platform: ProviderPlatform;
  upModelId?: string;
  upRequestUrl?: string;
  upStatusCode?: number;
  durationMs?: number;
  status: string;
  statusDescription?: string;
}

export interface UsageRecordDetailOutputDto {
  usageRecordId: string;
  // 下游字段（不变）
  downRequestUrl?: string;
  downRequestHeaders?: string;
  downRequestBody?: string;
  downResponseBody?: string;
  // 移除原 Up* 字段，改为 attempts 列表
  attempts: UsageRecordAttemptOutputDto[];
}
```

#### 2.3.7 前端列表页：重试 badge（usage-records.html）

```html
<!-- 在现有 p-tag 状态标签后追加 -->
@if (record.attemptCount > 1) {
  <p-tag
    [value]="'重试 ' + (record.attemptCount - 1) + '次'"
    severity="warn"
    styleClass="text-[10px] px-1.5 py-0.5 h-5"
    [pTooltip]="'共经历 ' + record.attemptCount + ' 次上游尝试'"
    tooltipPosition="right"
  ></p-tag>
}
```

#### 2.3.8 前端详情 Dialog：新增「尝试记录」Tab

```html
<!-- p-tablist 新增第三个 Tab -->
<p-tab value="2">
  尝试记录
  @if ((detail()?.attempts?.length || 0) > 1) {
    <span class="ml-1 text-orange-500">({{ detail()?.attempts?.length }})</span>
  }
</p-tab>

<!-- p-tabpanels 新增对应 TabPanel -->
<p-tabpanel value="2">
  <div class="flex flex-col gap-3 pt-4">
    @for (attempt of detail()?.attempts; track attempt.attemptNumber) {
      <div class="p-3 rounded-lg border"
           [ngClass]="attempt.status === 'Success'
             ? 'border-green-200 bg-green-50 dark:border-green-800 dark:bg-green-950/30'
             : 'border-red-200 bg-red-50 dark:border-red-800 dark:bg-red-950/30'">
        <div class="flex items-center justify-between gap-4 flex-wrap">
          <div class="flex items-center gap-2">
            <span class="text-xs font-bold text-muted-color w-6">#{{ attempt.attemptNumber }}</span>
            <p-tag
              [value]="getStatusLabel(attempt.status)"
              [severity]="getStatusSeverity(attempt.status)"
              styleClass="text-[10px] px-1.5 py-0.5 h-5"
            ></p-tag>
            @if (attempt.upStatusCode) {
              <p-tag
                [value]="attempt.upStatusCode.toString()"
                [severity]="attempt.upStatusCode | httpStatusSeverity"
                styleClass="text-[10px] px-1.5 py-0.5 h-5 font-mono"
              ></p-tag>
            }
          </div>
          <div class="flex items-center gap-4 text-xs text-muted-color">
            <span class="font-mono">{{ attempt.accountTokenName }}</span>
            <span>{{ attempt.durationMs }} ms</span>
          </div>
        </div>
        @if (attempt.statusDescription) {
          <p class="mt-2 text-xs text-orange-600 dark:text-orange-400">
            {{ attempt.statusDescription }}
          </p>
        }
        @if (attempt.upRequestUrl) {
          <p class="mt-1 text-xs font-mono text-muted-color truncate"
             [pTooltip]="attempt.upRequestUrl" tooltipPosition="top">
            {{ attempt.upRequestUrl }}
          </p>
        }
      </div>
    }
  </div>
</p-tabpanel>
```

---

## 三、实施计划

### Step 1：Domain 层 — 新实体 & 重构 UsageRecord

- [ ] 新增 `UsageRecordAttempt.cs`
- [ ] 新增 `UsageRecordAttemptDetail.cs`
- [ ] 重构 `UsageRecord.cs`：移除账号字段，新增 `AttemptCount`、`Attempts` 导航属性
- [ ] 调整 `UsageRecordDetail.cs`：移除 `Up*` 字段，仅保留 `Down*` 字段

### Step 2：Infrastructure 层 — EF Core 配置 & 迁移

- [ ] 调整 `UsageRecordEntityConfiguration.cs`：新增 `UsageRecordAttempt` 表配置、索引、关系
- [ ] 执行 `dotnet ef migrations add AddUsageRecordAttempt`
- [ ] 验证迁移 SQL 正确性

### Step 3：Application 层 — 写入路径重构

- [ ] 调整 `StartUsageInputDto.cs`：移除账号/Up 字段（仅保留客户端级信息）
- [ ] 新增 `AddAttemptInputDto.cs`（单次尝试数据）
- [ ] 调整 `FinishUsageInputDto.cs`：新增 `AttemptCount`、`DownResponseBody`
- [ ] 重构 `IUsageLifecycleAppService.cs`：新增 `AddAttemptAsync`
- [ ] 重构 `UsageLifecycleAppService.cs`：`AddAttemptAsync` 写入 `UsageRecordAttempt`
- [ ] 调整 `UsageRecordOutputDto.cs`：新增 `AttemptCount`，移除 `AccountTokenName/UpStatusCode/UpModelId`
- [ ] 新增 `UsageRecordAttemptOutputDto.cs`
- [ ] 调整 `UsageRecordDetailDto.cs`：移除 `Up*` 字段，新增 `Attempts` 列表
- [ ] 调整 `UsageRecordProfile.cs`：新增 Attempt 映射
- [ ] 调整 `UsageRecordAppService.GetDetailAsync`：Include `Attempts.Detail`

### Step 4：Api 层 — HostedService & Middleware 重构

- [ ] 重构 `AccountUsageRecordHostedService.cs`：
  - 新增 `UsageRecordAttemptItem` record
  - `ExecuteAsync` switch 新增 `UsageRecordAttemptItem` 分支，调用 `AddAttemptAsync`
- [ ] 重构 `SmartReverseProxyMiddleware.cs`：
  - 提升 `usageRecordId`、`attemptNumber`、`overallStopwatch` 到外层
  - `TryEnqueue(StartItem)` 在进入外层循环前调用（一次）
  - 内层 `finally` 改为入队 `UsageRecordAttemptItem`（每次尝试一条）
  - 外层 `finally` 入队 `UsageRecordEndItem`（含最终状态 + AttemptCount）

### Step 5：前端

- [ ] 调整 `usage.dto.ts`：新增 `attemptCount`、`UsageRecordAttemptOutputDto`、调整 `UsageRecordDetailOutputDto`
- [ ] 调整 `usage-records.html`：状态列新增重试 badge
- [ ] 调整 `usage-record-detail-dialog.ts`：新增「尝试记录」Tab

### Step 6：验证

- [ ] 单次请求（无重试）：1条 `UsageRecord` + 1条 `UsageRecordAttempt`，前端无重试 badge
- [ ] 同账号重试 2 次后成功：`AttemptCount=3`，前端显示「重试 2次」，详情 Tab 展示 3 条尝试
- [ ] 切换账号后成功：详情 Tab 展示完整跨账号重试链路
- [ ] 彻底失败：`UsageRecord.Status = Failed`，所有 Attempt 均为 Failed
- [ ] 长耗时请求中途：使用记录列表实时显示 `InProgress` 状态

---

## 四、查询能力对照

| 业务需求 | 查询方式 |
|------|------|
| 客户端请求成功率 | `UsageRecord.Status` 直接统计 |
| 某账号真实错误率 | `UsageRecordAttempt WHERE AccountTokenId = X` |
| 某账号贡献的成功请求数 | `UsageRecordAttempt WHERE AccountTokenId = X AND Status = Success` |
| 某请求完整重试链 | `UsageRecordAttempt WHERE UsageRecordId = Y ORDER BY AttemptNumber` |
| 重试率分析 | `UsageRecord WHERE AttemptCount > 1` |
| 账号平均响应耗时 | `AVG(UsageRecordAttempt.DurationMs) WHERE AccountTokenId = X` |
