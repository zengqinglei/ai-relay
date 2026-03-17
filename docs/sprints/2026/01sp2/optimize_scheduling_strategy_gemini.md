# 智能反向代理调度与重试机制优化方案

> **创建时间**: 2026-01-29
> **最后更新**: 2026-01-31
> **状态**: ✅ 设计完成，待实施
> **版本**: v5.0 (终定版)
> **预计工期**: 9 天
> **优先级**: P0 (高优先级)

## 📋 执行摘要

本方案旨在通过中间件驱动模式，构建高性能、高可用的智能反向代理系统，解决以下核心问题：

1. **无重试机制** → 通过 `SmartReverseProxyMiddleware` 实现 429/5xx 自动重试
2. **限流处理简单** → 三级降级策略 (retry-after 解析 + 指数退避 + 配额预测)
3. **无配额管理** → 后台服务定时刷新 + QuotaPriority 调度策略
4. **静态路由限制** → 移除 YARP 配置驱动，代码级实现动态 BaseUrl 切换

**核心技术方案**：
- **纯代码驱动**: 移除 `appsettings.json` 中的 YARP 配置，使用 `app.Map(...)` + `SmartReverseProxyMiddleware`.
- **策略模式**: 解耦平台逻辑，支持动态 BaseUrl.
- **分布式限流**: Redis TTL + 数据库持久化双重保障.
- **三级降级**: 智能解析 Retry-After，实现最大 1 小时的长时限流.

**适用范围**：全平台 (ANTIGRAVITY, GEMINI, CLAUDE, OPENAI)

---

## 1. 需求背景

当前 `ai-relay` 项目的反向代理模块基于 YARP 的配置驱动模式 (`MapReverseProxy`)，其核心调度逻辑耦合在 YARP 的 `RequestTransform` 阶段。这种架构存在以下痛点：

*   **缺乏高可用性**: 这是一个"一次性"的转发过程。如果选中的账号遇到瞬时错误（如 429 Too Many Requests 或 503 Service Unavailable），请求会直接失败，无法自动切换到其他健康账号重试。
*   **调度策略单一**: 仅支持基础的轮询或随机，无法感知账号在"上一秒"是否刚触发了限流。
*   **扩展性受限**: 不同平台（Antigravity, Gemini, Claude）的协议适配逻辑与核心转发逻辑纠缠在一起。
*   **统计滞后**: 使用 `OnCompleted` 回调记录流量统计不够可靠，且难以与重试逻辑整合。
*   **路由静态化**: 无法根据选中的账号动态调整上游地址（BaseUrl），限制了多区域或自定义代理的支持。
*   **配置繁琐**: `appsettings.json` 维护大量静态路由和集群配置，不易扩展。

**目标**: 参考 `Antigravity-Manager` 的成熟实现，构建一套**"通用智能网关"**体系。通过**中间件接管转发流程**，在应用层实现**3次弹性重试**、**动态路由**、**分布式限流熔断**、**配额管理**以及**平台策略解耦**，适用于所有接入平台。

---

## 2. 核心策略

### 2.1 设计理念

1.  **中间件驱动循环 (Middleware-Driven Loop)**: 放弃 YARP 的 `RequestTransform` 选号模式，改用自定义的 `SmartReverseProxyMiddleware` 完全接管转发流程。在此中间件中实现 `while(retry < 3)` 循环。
2.  **策略模式 (Strategy Pattern)**: 将"通用转发流程"与"平台特定逻辑"解耦。
    *   **通用流程**: 选号 -> 转发 -> 检查结果 -> (成功/重试/失败) -> 统计。
    *   **平台策略**: 通过 `IProxyPlatformStrategy` 处理动态 URL 确定、URL 重写、Header 伪装、Body 注入（如 ProjectId）及响应签名提取。
3.  **分布式限流熔断 (Distributed Circuit Breaker)**:
    *   **Redis TTL + 数据库持久化**双重机制，确保分布式一致性。
    *   使用 `IDistributedCache` (支持 Redis) 实现毫秒级状态同步。
    *   当账号报 429 时，在 Redis 中设置带过期时间的 Key (Key: `RateLimit:{AccountId}`)，同时写入数据库持久化状态。
    *   多实例部署时，所有节点都能感知并避开被限流的账号。
4.  **三级降级策略 (Three-Level Degradation)**:
    *   **Level 1**: 解析 `Retry-After` 响应头或错误消息中的等待时间（优先级最高，准确性最高）。
    *   **Level 2**: 指数退避算法 (5s → 3600s)，公式: `Delay = min(BaseDelay * 2^(ErrorCount), MaxDelay)`。
    *   **Level 3**: 配额预测降级（后台服务定时刷新配额，QuotaPriority 策略优先选择高配额账号）。
    *   **Jitter**: ±20% 随机偏移，避免惊群效应。
5.  **智能成功解锁机制 (Intelligent Success Unlock)**:
    *   连续 N 次成功后，提前解锁限流状态，提高账号利用率。
    *   采用**滑动窗口计数器**避免惊群效应。
    *   解锁条件：(连续成功次数 >= 3) && (距离上次失败 >= 60s)。
6.  **标准化异常**:
    *   当所有重试均失败或无号可用时，抛出 `Leistd.Exception.Core.InternalServerException` (500)，并附带详细错误信息 "No available provider accounts found after retries."。
7.  **显式路由映射**:
    *   使用 `app.Map(...)` 配合 `PlatformMetadata` 在代码中定义路由，替代 `appsettings.json` 配置，支持动态 BaseUrl。

### 2.2 调整内容树形目录结构

```text
backend/src/
│   ├── AiRelay.Domain\
│   │   └── Shared\
│   │       └── Scheduling\
│   │           ├── IRateLimitTracker.cs             // [新增] 接口
│   │           └── RateLimitTracker.cs              // [新增] Redis TTL + 数据库持久化实现
│   │
│   ├── AiRelay.Application\
│   │   ├── ProviderAccounts\
│   │   │   └── AppServices\
│   │   │       ├── IAccountTokenAppService.cs       // [修改] 增加 excludedIds 参数
│   │   │       └── AccountTokenAppService.cs        // [修改] 过滤逻辑
│   │   │
│   │   └── ProviderGroups\AppServices\
│   │       └── ProviderGroupAppService.cs            // [新增] 策略兼容性验证
│   │
│   ├── AiRelay.Api\
│   │   ├── Middleware\
│   │   │   └── SmartProxy\
│   │   │       ├── SmartReverseProxyMiddleware.cs   // [新增] 核心中间件
│   │   │       ├── PlatformMetadata.cs              // [新增] 路由元数据
│   │   │       └── Strategies\
│   │   │           ├── IProxyPlatformStrategy.cs    // [新增] 策略接口
│   │   │           ├── BaseProxyStrategy.cs         // [新增] 基类
│   │   │           ├── AntigravityProxyStrategy.cs  // [新增] 策略实现
│   │   │           ├── GeminiProxyStrategy.cs       // [新增] 策略实现
│   │   │           ├── ClaudeProxyStrategy.cs       // [新增] 策略实现
│   │   │           └── OpenAiProxyStrategy.cs       // [新增] 策略实现
│   │   │
│   │   ├── BackgroundServices\
│   │   │   └── AccountQuotaRefreshBackgroundService.cs  // [新增] 配额刷新后台服务
│   │   │
│   │   └── Program.cs                               // [修改] 注册服务与路由
│   │
│   └── AiRelay.Infrastructure\
│       └── Shared\ExternalServices\ChatModel\Client\
│           ├── IChatModelClient.cs                   // [修改] 添加配额接口
│           ├── AntigravityChatModelClient.cs         // [修改] 实现配额接口
│           ├── GeminiAccountChatModelClient.cs       // [修改] 实现配额接口
│           ├── ClaudeChatModelClient.cs              // [修改] 空实现
│           ├── OpenAiChatModelClient.cs              // [修改] 空实现
│           └── GeminiApiChatModelClient.cs           // [修改] 空实现

frontend-gemini/src/app/features/platform/
├── models/
│   └── provider-group.dto.ts                         // [修改] 添加 QuotaPriority 枚举
│
└── components/provider-group/widgets/
    └── group-edit-dialog/
        └── group-edit-dialog.ts                      // [修改] 动态策略选项
```

### 2.3 配置调整 (`appsettings.json`)

我们将**移除**原有 YARP `ReverseProxy` 整个配置块，因为路由和 HTTP 客户端配置将由代码动态处理。

```json
// 移除以下配置
// "ReverseProxy": { ... }

// 可选：保留自定义超时配置
"AiProxyOptions": {
  "TimeoutSeconds": 120,
  "MaxConnectionsPerServer": 1000
}
```

### 2.4 核心请求链路流程 (Core Request Lifecycle)

以下清单展示了请求在中间件中的完整流转步骤及 `RateLimitTracker` 的具体介入点：

1.  **请求进入 (Request Entry)**
    *   通过 `app.Map` 匹配到 `SmartReverseProxyMiddleware`。
    *   **Context Prep**: 提取 SessionHash，识别 PlatformMetadata。
    *   *注*: `EnableBuffering` 仅在首次失败且确定需要重试时按需开启，减少内存开销。

2.  **重试循环 (Retry Loop)** - *最多执行 3 次*
    *   **Step 1: 智能选号 (Select Account)**
        *   调用 `SelectAccountAsync`。
        *   **Tracker 介入**: `AccountTokenAppService` 内部调用 `RateLimitTracker.IsRateLimitedAsync(id)`，直接过滤掉被锁定的账号。
        *   **过滤规则**: 排除 `excludedIds` (本轮已失败账号) + 排除 `IsRateLimitedAsync` 为 true 的账号。
        *   *异常分支*: 若无可用账号且重试耗尽 -> 抛出 `InternalServerException` (500)。
    
    *   **Step 2: 动态路由与适配 (Routing & Adaptation)**
        *   **动态 BaseUrl**: 获取 `account.BaseUrl`，若为空则使用策略默认值。
        *   根据平台策略 (`IProxyPlatformStrategy`) 修改请求。
        *   操作: 注入 ProjectId, 修改 Auth Header, 重写 URL。
    
    *   **Step 3: 转发请求 (Forwarding)**
        *   通过 `IHttpForwarder.SendAsync` 发送请求至上游。
    
    *   **Step 4: 结果判定 (Decision Making)**
        *   **分支 A: 成功 (Success 2xx)**
            *   **Tracker 介入**: 调用 `RateLimitTracker.MarkSuccessAsync`。
            *   策略: `ProcessResponseAsync` (提取流式签名等)。
            *   统计: `RecordUsageAsync`。
            *   **动作**: 返回响应给客户端 -> **结束循环**。
        
        *   **分支 B: 可重试错误 (Retryable 429/503)**
            *   **开启 Buffering**: 此时调用 `context.Request.EnableBuffering()` 并重置流位置，准备下一次尝试。
            *   策略: 解析 `Retry-After` 或计算指数退避时间。
            *   **Tracker 介入**: 调用 `RateLimitTracker.LockAsync`，在 Redis 中设置带过期时间的锁。
            *   上下文: 将账号加入 `excludedIds`。
            *   **动作**: `continue` -> **进入下一次循环**。
        
        *   **分支 C: 致命错误 (Fatal 401/403)**
            *   DB: `DisableAccountAsync` 禁用账号。
            *   **开启 Buffering**: 同上。
            *   上下文: 将账号加入 `excludedIds`。
            *   **动作**: `continue` -> **进入下一次循环**。
        
        *   **分支 D: 其他错误 (400/404/500)**
            *   **动作**: 原样返回错误 -> **结束循环**。

---

## 3. 实施细节与核心代码

### 3.1 路由配置与服务注册 (`Program.cs`)

```csharp
// 1. 注册核心转发器 (YARP 核心组件)
builder.Services.AddHttpForwarder();

// 2. 注册全局 HTTP 消息调用器 (替代原来的 Cluster 配置)
builder.Services.AddSingleton<HttpMessageInvoker>(sp =>
{
    var handler = new SocketsHttpHandler
    {
        UseProxy = false,
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.None,
        UseCookies = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        MaxConnectionsPerServer = 1000,
    };
    return new HttpMessageInvoker(handler);
});

// ... 其他服务注册 ...

var app = builder.Build();

// ... 中间件管道 ...

// 3. 显式路由映射
var proxyHandler = (HttpContext context) => 
    context.RequestServices.GetRequiredService<SmartReverseProxyMiddleware>().InvokeAsync(context);

app.Map("/gemini/{**catch-all}", proxyHandler)
   .WithMetadata(new PlatformMetadata(ProviderPlatform.GEMINI_ACCOUNT));

app.Map("/gemini-api/{**catch-all}", proxyHandler)
   .WithMetadata(new PlatformMetadata(ProviderPlatform.GEMINI_API));

app.Map("/claude/{**catch-all}", proxyHandler)
   .WithMetadata(new PlatformMetadata(ProviderPlatform.CLAUDE));

app.Map("/antigravity/{**catch-all}", proxyHandler)
   .WithMetadata(new PlatformMetadata(ProviderPlatform.ANTIGRAVITY));

app.Run();
```

### 3.2 `SmartReverseProxyMiddleware.InvokeAsync` (核心逻辑)

```csharp
public async Task InvokeAsync(HttpContext context)
{
    // 1. 识别平台
    var metadata = context.GetEndpoint()?.Metadata.GetMetadata<PlatformMetadata>();
    if (metadata == null) { await _next(context); return; }
    
    var strategy = _strategyFactory.GetStrategy(metadata.Platform);

    // 2. 重试循环
    const int MaxRetries = 3;
    for (int retry = 0; retry < MaxRetries; retry++)
    {
        AvailableAccountTokenOutputDto? account = null;
        try 
        { 
            // 选号：AppService 内部会调用 _rateLimitTracker.IsRateLimitedAsync 进行过滤
            account = await _accountService.SelectAccountAsync(platformId, apiKeyId, sessionHash, excludedIds);
        } 
        catch (Exception) { /* ... */ }

        // 3. 动态 BaseUrl 确定
        var destinationPrefix = !string.IsNullOrEmpty(account.BaseUrl) 
            ? account.BaseUrl 
            : strategy.GetDefaultBaseUrl();

        // 4. 获取转换器
        var transformer = strategy.CreateTransformer(account);
        
        // 5. 执行转发
        var error = await _forwarder.SendAsync(
            context, 
            destinationPrefix, 
            _httpClient, 
            _forwarderConfig, 
            transformer);

        // 6. 结果判定
        if (error == ForwarderError.None && context.Response.StatusCode < 400)
        {
            // 成功：标记成功
            await _rateLimitTracker.MarkSuccessAsync(account.Id);
            // ... 后续处理 ...
            return;
        }

        // 失败处理
        if (strategy.IsRetryableError(context.Response.StatusCode)) // 429, 503
        {
            // ... 解析 delay (调用 HandleRateLimitWithDegradationAsync) ...
            
            // 锁定账号：写入 Redis 锁
            await _rateLimitTracker.LockAsync(account.Id, delay);
            excludedIds.Add(account.Id);
            
            // ... 重试准备 (EnableBuffering, Reset Stream) ...
            continue;
        }
        // ... 
    }
}
```

### 3.3 `RateLimitTracker` (Redis TTL + 数据库持久化)

```csharp
public class RateLimitTracker : IRateLimitTracker
{
    private readonly IDistributedCache _cache;
    private readonly IRepository<AccountToken, Guid> _accountRepository;
    private readonly ILogger<RateLimitTracker> _logger;

    private const int SUCCESS_UNLOCK_THRESHOLD = 3;  // 连续成功3次触发解锁
    private const int SUCCESS_UNLOCK_COOLDOWN_SECONDS = 60;  // 距离上次失败至少60秒

    // 1. LockAsync: 核心熔断机制 (Redis TTL + 数据库持久化)
    public async Task LockAsync(Guid accountId, TimeSpan duration, CancellationToken cancellationToken = default)
    {
        var key = $"RateLimit:{accountId}";
        var lockInfo = new RateLimitLockInfo
        {
            LockedAt = DateTime.UtcNow,
            UnlockAt = DateTime.UtcNow.Add(duration),
            Reason = "Rate limited by upstream API"
        };

        // 1.1 立即写入 Redis (毫秒级同步)
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = duration
        };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(lockInfo), options, cancellationToken);
        _logger.LogInformation("账号 {AccountId} 已锁定 {Seconds} 秒 (Redis)", accountId, duration.TotalSeconds);

        // 1.2 异步写入数据库 (持久化备份)
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account != null)
        {
            account.MarkAsRateLimited((int)duration.TotalSeconds, $"429 限流，锁定至 {lockInfo.UnlockAt:yyyy-MM-dd HH:mm:ss}");
            await _accountRepository.UpdateAsync(account, cancellationToken);
            _logger.LogDebug("账号 {AccountId} 限流状态已持久化到数据库", accountId);
        }
    }

    // 2. IsRateLimitedAsync: 选号时的过滤依据 (快速路径 Redis + 慢速路径数据库)
    public async Task<bool> IsRateLimitedAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var key = $"RateLimit:{accountId}";

        // 快速路径: 检查 Redis
        var cachedValue = await _cache.GetStringAsync(key, cancellationToken);
        if (!string.IsNullOrEmpty(cachedValue))
        {
            try
            {
                var lockInfo = JsonSerializer.Deserialize<RateLimitLockInfo>(cachedValue);
                if (lockInfo != null && lockInfo.UnlockAt > DateTime.UtcNow)
                {
                    _logger.LogDebug("账号 {AccountId} 仍在限流期 (Redis)，解锁时间: {UnlockAt}",
                        accountId, lockInfo.UnlockAt);
                    return true;
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "解析 Redis 限流信息失败，回退到数据库检查");
            }
        }

        // 慢速路径: 检查数据库 (Redis 缺失或过期时)
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account != null && account.Status == AccountStatus.RateLimited)
        {
            // 检查数据库中的 LockedUntil 是否已过期
            if (account.LockedUntil.HasValue && account.LockedUntil.Value > DateTime.UtcNow)
            {
                _logger.LogDebug("账号 {AccountId} 仍在限流期 (数据库)，解锁时间: {LockedUntil}",
                    accountId, account.LockedUntil.Value);
                return true;
            }
            else
            {
                // 数据库中限流已过期，但 Redis 未同步，自动恢复状态
                account.ResetStatus();
                await _accountRepository.UpdateAsync(account, cancellationToken);
                _logger.LogInformation("账号 {AccountId} 限流已过期，自动恢复状态", accountId);
            }
        }

        return false;
    }

    // 3. MarkSuccessAsync: 智能成功解锁机制
    // 设计理念:
    // 1. 基于滑动窗口计数器，记录连续成功次数
    // 2. 解锁条件: (连续成功 >= 3) && (距离上次失败 >= 60s)
    // 3. 避免在限流窗口期内贸然解锁导致再次被封
    public async Task MarkSuccessAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var successKey = $"RateLimit:Success:{accountId}";
        var rateLimitKey = $"RateLimit:{accountId}";

        // 检查是否在限流期
        var isLimited = await IsRateLimitedAsync(accountId, cancellationToken);
        if (!isLimited)
        {
            // 未限流时清空成功计数器
            await _cache.RemoveAsync(successKey, cancellationToken);
            return;
        }

        // 获取限流信息
        var lockInfoJson = await _cache.GetStringAsync(rateLimitKey, cancellationToken);
        if (string.IsNullOrEmpty(lockInfoJson))
            return;

        var lockInfo = JsonSerializer.Deserialize<RateLimitLockInfo>(lockInfoJson);
        if (lockInfo == null)
            return;

        // 检查冷却时间 (距离上次失败至少 60 秒)
        var timeSinceFailure = (DateTime.UtcNow - lockInfo.LockedAt).TotalSeconds;
        if (timeSinceFailure < SUCCESS_UNLOCK_COOLDOWN_SECONDS)
        {
            _logger.LogDebug("账号 {AccountId} 距离失败时间过短 ({Elapsed}s < {Required}s)，暂不解锁",
                accountId, timeSinceFailure, SUCCESS_UNLOCK_COOLDOWN_SECONDS);
            return;
        }

        // 增加成功计数
        var successCountStr = await _cache.GetStringAsync(successKey, cancellationToken) ?? "0";
        var successCount = int.Parse(successCountStr) + 1;

        await _cache.SetStringAsync(
            successKey,
            successCount.ToString(),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            },
            cancellationToken);

        // 检查是否达到解锁阈值
        if (successCount >= SUCCESS_UNLOCK_THRESHOLD)
        {
            _logger.LogInformation(
                "账号 {AccountId} 连续成功 {Count} 次 (>= {Threshold})，提前解锁",
                accountId, successCount, SUCCESS_UNLOCK_THRESHOLD);

            // 移除 Redis 锁
            await _cache.RemoveAsync(rateLimitKey, cancellationToken);
            await _cache.RemoveAsync(successKey, cancellationToken);

            // 更新数据库状态
            var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
            if (account != null && account.Status == AccountStatus.RateLimited)
            {
                account.ResetStatus();
                await _accountRepository.UpdateAsync(account, cancellationToken);
                _logger.LogInformation("账号 {AccountId} 状态已恢复为 Normal", accountId);
            }
        }
        else
        {
            _logger.LogDebug("账号 {AccountId} 连续成功 {Count}/{Threshold} 次",
                accountId, successCount, SUCCESS_UNLOCK_THRESHOLD);
        }
    }
}

public record RateLimitLockInfo
{
    public DateTime LockedAt { get; init; }
    public DateTime UnlockAt { get; init; }
    public string Reason { get; init; } = string.Empty;
}
```

### 3.3 `AccountTokenAppService.SelectAccountAsync` (集成逻辑)

```csharp
public async Task<AvailableAccountTokenOutputDto> SelectAccountAsync(..., IEnumerable<Guid> excludedIds)
{
    // 1. 获取所有可用账号 (DB/Cache)
    var accounts = await _repository.GetListAsync(...);
    
    // 2. 内存/Redis 过滤
    var validAccounts = new List<AvailableAccountTokenOutputDto>();
    foreach (var acc in accounts)
    {
        // 排除本轮已失败的
        if (excludedIds.Contains(acc.Id)) continue;
        
        // 排除被熔断锁定的
        if (await _rateLimitTracker.IsRateLimitedAsync(acc.Id)) continue;
        
        validAccounts.Add(acc);
    }
    
    // 3. 负载均衡/粘性会话逻辑 (从 validAccounts 中选择)
    // ...
}
```

### 3.4 三级降级策略实现 (参考 Claude 方案优化)

#### 3.4.1 策略说明

| 级别 | 数据源 | 优先级 | 准确性 | 锁定时间范围 | 说明 |
|------|--------|--------|--------|------------|------|
| **Level 1** | API 响应头/错误消息 | 最高 | ✅ 准确 | 5s - 3600s | 解析 `retry-after` 或错误信息中的等待时间 |
| **Level 2** | 指数退避 | 兜底 | ⚠️ 估算 | 5s - 3600s | 基于连续错误次数计算，±20% Jitter |
| **Level 3** | 配额预测 | 辅助 | ℹ️ 参考 | N/A | 后台服务定时刷新，用于 QuotaPriority 策略 |

**关键改进**：
- ✅ **MaxDelay 从 5s 提升到 3600s**，支持长时限流（如 Google API 的 2 小时限流）
- ✅ **采用 Claude 方案的完整 JSON 解析逻辑**（支持 Google API `RetryInfo`、`quotaResetDelay`、`quotaResetTimeStamp`）
- ✅ **添加 Jitter (±20%) 避免惊群效应**

#### 3.4.2 核心实现代码

**文件**：`backend/src/AiRelay.Api/Middleware/SmartProxy/SmartReverseProxyMiddleware.cs` (部分)

```csharp
/// <summary>
/// 处理限流，应用三级降级策略
/// </summary>
private async Task<int> HandleRateLimitWithDegradationAsync(
    Guid accountId,
    string? errorMessage,
    CancellationToken cancellationToken)
{
    // Level 1: 从错误消息或响应头中提取 retry-after (参考 Claude 方案)
    var retryAfterSeconds = ExtractRetryAfterFromError(errorMessage);
    if (retryAfterSeconds.HasValue && retryAfterSeconds.Value > 0)
    {
        _logger.LogInformation(
            "Level 1: API retry-after - AccountId: {AccountId}, Seconds: {Seconds}s",
            accountId, retryAfterSeconds.Value);
        return retryAfterSeconds.Value;
    }

    // Level 2: 指数退避 (兜底)
    var errorCount = await _usageCacheService.GetErrorCountAsync(accountId, cancellationToken);
    var backoffSeconds = CalculateExponentialBackoff(errorCount);

    _logger.LogWarning(
        "Level 2: 指数退避 - AccountId: {AccountId}, Seconds: {Seconds}s, 连续错误: {Count}",
        accountId, backoffSeconds, errorCount);

    return backoffSeconds;
}

/// <summary>
/// 从错误消息中提取 retry-after (秒) - 完整实现
/// </summary>
/// <remarks>
/// 支持多种格式（与 Claude 方案一致）：
/// 1. JSON 结构 (Google API): RetryInfo.retryDelay, metadata.quotaResetDelay, metadata.quotaResetTimeStamp
/// 2. 正则匹配: retry-after, x-ratelimit-reset 等
/// 3. 时长格式: "8085.070001278s", "2h14m45s"
/// </remarks>
private int? ExtractRetryAfterFromError(string? errorMessage)
{
    if (string.IsNullOrEmpty(errorMessage))
        return null;

    // 1. 尝试解析 JSON 结构 (优先级最高)
    try
    {
        var jsonMatch = Regex.Match(errorMessage, @"\{.*\}", RegexOptions.Singleline);
        if (jsonMatch.Success)
        {
            using var doc = JsonDocument.Parse(jsonMatch.Value);
            var root = doc.RootElement;

            // 1.1 检查 error.details[].retryDelay (Google API 格式)
            if (root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("details", out var details) &&
                details.ValueKind == JsonValueKind.Array)
            {
                foreach (var detail in details.EnumerateArray())
                {
                    // 检查 RetryInfo 类型
                    if (detail.TryGetProperty("@type", out var type) &&
                        type.GetString()?.Contains("RetryInfo") == true &&
                        detail.TryGetProperty("retryDelay", out var retryDelay))
                    {
                        var delayStr = retryDelay.GetString();
                        var seconds = ParseDurationToSeconds(delayStr);
                        if (seconds.HasValue)
                        {
                            _logger.LogDebug("从 RetryInfo.retryDelay 解析: {Seconds}s", seconds.Value);
                            return seconds.Value;
                        }
                    }

                    // 检查 metadata.quotaResetDelay
                    if (detail.TryGetProperty("metadata", out var metadata) &&
                        metadata.TryGetProperty("quotaResetDelay", out var quotaResetDelay))
                    {
                        var delayStr = quotaResetDelay.GetString();
                        var seconds = ParseDurationToSeconds(delayStr);
                        if (seconds.HasValue)
                        {
                            _logger.LogDebug("从 metadata.quotaResetDelay 解析: {Seconds}s", seconds.Value);
                            return seconds.Value;
                        }
                    }

                    // 检查 metadata.quotaResetTimeStamp
                    if (detail.TryGetProperty("metadata", out var meta2) &&
                        meta2.TryGetProperty("quotaResetTimeStamp", out var resetTimestamp))
                    {
                        var timestampStr = resetTimestamp.GetString();
                        if (DateTime.TryParse(timestampStr, out var resetTime))
                        {
                            var seconds = (int)Math.Ceiling((resetTime - DateTime.UtcNow).TotalSeconds);
                            if (seconds > 0)
                            {
                                _logger.LogDebug("从 quotaResetTimeStamp 解析: {Seconds}s", seconds);
                                return Math.Min(seconds, 3600);
                            }
                        }
                    }
                }
            }

            // 1.2 检查顶层 retry-after 字段
            if (root.TryGetProperty("retry-after", out var retryAfter))
            {
                if (retryAfter.ValueKind == JsonValueKind.Number)
                {
                    return Math.Min(retryAfter.GetInt32(), 3600);
                }
                else if (retryAfter.ValueKind == JsonValueKind.String)
                {
                    var seconds = ParseDurationToSeconds(retryAfter.GetString());
                    if (seconds.HasValue)
                        return seconds.Value;
                }
            }
        }
    }
    catch (JsonException ex)
    {
        _logger.LogTrace(ex, "JSON 解析失败，尝试正则匹配");
    }

    // 2. 正则匹配 (兜底) - 与 Claude 方案一致
    var patterns = new[]
    {
        @"retryDelay[""']?\s*:\s*[""']?(\d+(?:\.\d+)?)[""']?s",  // retryDelay: "8085.070001278s"
        @"quotaResetDelay[""']?\s*:\s*[""']?([\dhms.]+)[""']?",  // quotaResetDelay: "2h14m45.070001278s"
        @"retry[-_]?after[:\s=]+(\d+)",                          // retry-after: 60
        @"retry\s+after\s+(\d+)\s*seconds?",                     // retry after 60 seconds
        @"wait\s+(\d+)\s*seconds?",                              // wait 60 seconds
        @"(\d+)\s*seconds?\s+later",                             // 60 seconds later
        @"x-ratelimit-reset:\s*(\d+)"                            // x-ratelimit-reset: 1234567890
    };

    foreach (var pattern in patterns)
    {
        var match = Regex.Match(errorMessage, pattern, RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var value = match.Groups[1].Value;

            // 尝试解析为秒数
            if (int.TryParse(value, out var intValue))
            {
                // 如果值很大，可能是 Unix 时间戳
                if (intValue > 1000000000)
                {
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(intValue).UtcDateTime;
                    var seconds = (int)Math.Ceiling((resetTime - DateTime.UtcNow).TotalSeconds);
                    return Math.Max(5, Math.Min(seconds, 3600));
                }
                return Math.Max(5, Math.Min(intValue, 3600));
            }

            // 尝试解析为时长格式 (如 "2h14m45s")
            var durationSeconds = ParseDurationToSeconds(value);
            if (durationSeconds.HasValue)
                return durationSeconds.Value;
        }
    }

    return null;
}

/// <summary>
/// 解析时长字符串为秒数
/// </summary>
private int? ParseDurationToSeconds(string? duration)
{
    if (string.IsNullOrEmpty(duration))
        return null;

    try
    {
        // 格式 1: "8085.070001278s" (纯秒数)
        if (duration.EndsWith("s") && !duration.Contains("h") && !duration.Contains("m"))
        {
            var secondsStr = duration.TrimEnd('s');
            if (double.TryParse(secondsStr, out var seconds))
            {
                return Math.Max(5, Math.Min((int)Math.Ceiling(seconds), 3600));
            }
        }

        // 格式 2: "2h14m45.070001278s" (复合格式)
        int totalSeconds = 0;

        // 提取小时
        var hourMatch = Regex.Match(duration, @"(\d+)h");
        if (hourMatch.Success)
        {
            totalSeconds += int.Parse(hourMatch.Groups[1].Value) * 3600;
        }

        // 提取分钟
        var minuteMatch = Regex.Match(duration, @"(\d+)m");
        if (minuteMatch.Success)
        {
            totalSeconds += int.Parse(minuteMatch.Groups[1].Value) * 60;
        }

        // 提取秒数
        var secondMatch = Regex.Match(duration, @"(\d+(?:\.\d+)?)s");
        if (secondMatch.Success)
        {
            totalSeconds += (int)Math.Ceiling(double.Parse(secondMatch.Groups[1].Value));
        }

        if (totalSeconds > 0)
        {
            return Math.Max(5, Math.Min(totalSeconds, 3600));
        }
    }
    catch (Exception ex)
    {
        _logger.LogTrace(ex, "解析时长失败: {Duration}", duration);
    }

    return null;
}

/// <summary>
/// 指数退避算法 (优化版，最大值提升到 3600s)
/// </summary>
private int CalculateExponentialBackoff(int errorCount)
{
    const int baseSeconds = 5;
    const int maxSeconds = 3600;  // 修复：从 5s 提升到 3600s (1 小时)

    // 指数增长: 5, 10, 20, 40, 80, 160, 320, 640, 1280, 2560, 3600
    var backoffSeconds = baseSeconds * Math.Pow(2, Math.Min(errorCount, 10));

    // ±20% Jitter 避免惊群效应
    var jitter = Random.Shared.NextDouble() * 0.4 - 0.2;
    backoffSeconds *= (1 + jitter);

    return (int)Math.Min(backoffSeconds, maxSeconds);
}
```

**退避时间表（优化后）**：

| 错误次数 | 基础值 | 实际范围 (±20%) | 说明 |
|---------|--------|----------------|------|
| 0 | 5s | 4-6s | 初次失败 |
| 1 | 10s | 8-12s | |
| 2 | 20s | 16-24s | |
| 3 | 40s | 32-48s | |
| 4 | 80s | 64-96s | |
| 5 | 160s | 128-192s | |
| 6 | 320s | 256-384s | |
| 7 | 640s | 512-768s | |
| 8 | 1280s | 1024-1536s | ~21 分钟 |
| 9 | 2560s | 2048-3072s | ~42 分钟 |
| 10+ | 3600s | 2880-4320s | **1 小时上限** |

### 3.5 配额管理实现 (Level 3)

#### 3.5.1 配额 DTO

**文件**：`backend/src/AiRelay.Domain/Shared/ExternalServices/ChatModel/Dto/AccountQuotaInfo.cs`

```csharp
public record AccountQuotaInfo
{
    public int? RemainingQuota { get; init; }
    public string? QuotaResetTime { get; init; }
    public string? SubscriptionTier { get; init; }
    public DateTime? LastRefreshed { get; init; }
}
```

#### 3.5.2 后台刷新服务

**文件**：`backend/src/AiRelay.Api/BackgroundServices/AccountQuotaRefreshBackgroundService.cs`

```csharp
public class AccountQuotaRefreshBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AccountQuotaRefreshBackgroundService> _logger;
    private const int REFRESH_INTERVAL_MINUTES = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("配额刷新后台服务已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAllQuotasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "配额刷新任务执行失败");
            }

            await Task.Delay(TimeSpan.FromMinutes(REFRESH_INTERVAL_MINUTES), stoppingToken);
        }
    }

    private async Task RefreshAllQuotasAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var accountRepo = scope.ServiceProvider.GetRequiredService<IRepository<AccountToken, Guid>>();
        var clientFactory = scope.ServiceProvider.GetRequiredService<IChatModelClientFactory>();
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        // 仅刷新支持配额查询的平台
        var accounts = await accountRepo.GetListAsync(
            a => (a.Platform == ProviderPlatform.ANTIGRAVITY ||
                  a.Platform == ProviderPlatform.GEMINI_ACCOUNT) &&
                 a.IsActive && !a.IsDeleted,
            cancellationToken);

        _logger.LogInformation("开始刷新配额，账号数量: {Count}", accounts.Count);

        int successCount = 0, failureCount = 0;

        foreach (var account in accounts)
        {
            try
            {
                var client = clientFactory.CreateClient(account);
                var quotaInfo = await client.FetchQuotaAsync(cancellationToken);

                if (quotaInfo != null)
                {
                    var cacheKey = $"account:quota:{account.Id}";
                    await cache.SetStringAsync(
                        cacheKey,
                        JsonSerializer.Serialize(quotaInfo),
                        new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                        },
                        cancellationToken);

                    successCount++;
                }
            }
            catch (Exception ex)
            {
                failureCount++;
                _logger.LogWarning(ex, "刷新账号配额失败: {Name}", account.Name);
            }
        }

        _logger.LogInformation("配额刷新完成 - 成功: {Success}, 失败: {Failure}",
            successCount, failureCount);
    }
}
```

#### 3.5.3 QuotaPriority 策略

**文件**：`backend/src/AiRelay.Domain/ProviderGroups/DomainServices/QuotaPriorityStrategy.cs`

```csharp
public class QuotaPriorityStrategy : IGroupSchedulingStrategy
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<QuotaPriorityStrategy> _logger;

    public async Task<ProviderGroupAccountRelation?> SelectAccountAsync(
        IReadOnlyList<ProviderGroupAccountRelation> relations,
        Func<IEnumerable<Guid>, Task<Dictionary<Guid, long>>> usageProvider)
    {
        if (relations.Count == 0) return null;

        // 从缓存获取配额信息
        var quotaInfos = new Dictionary<Guid, int>();

        foreach (var relation in relations)
        {
            var cacheKey = $"account:quota:{relation.AccountTokenId}";
            var cachedData = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedData))
            {
                try
                {
                    var quotaInfo = JsonSerializer.Deserialize<AccountQuotaInfo>(cachedData);
                    quotaInfos[relation.AccountTokenId] = quotaInfo?.RemainingQuota ?? 0;
                }
                catch
                {
                    quotaInfos[relation.AccountTokenId] = 0;
                }
            }
            else
            {
                quotaInfos[relation.AccountTokenId] = 0;
            }
        }

        // 按配额降序，配额相同时按优先级
        var selected = relations
            .OrderByDescending(r => quotaInfos.GetValueOrDefault(r.AccountTokenId, 0))
            .ThenBy(r => r.Priority)
            .FirstOrDefault();

        if (selected != null)
        {
            _logger.LogDebug("QuotaPriority 选择账号: {AccountId}, 配额: {Quota}",
                selected.AccountTokenId,
                quotaInfos.GetValueOrDefault(selected.AccountTokenId, 0));
        }

        return selected;
    }
}
```

#### 3.5.4 前端动态策略选项

**文件**：`frontend-gemini/src/app/features/platform/components/provider-group/widgets/group-edit-dialog/group-edit-dialog.ts`

```typescript
// 根据平台动态显示策略选项
get availableStrategies() {
  const platform = this.form.get('platform')?.value;

  const baseStrategies = [
    { value: GroupSchedulingStrategy.LeastRequests, label: '最少请求数' },
    { value: GroupSchedulingStrategy.WeightedRandom, label: '加权随机' },
    { value: GroupSchedulingStrategy.Priority, label: '优先级' }
  ];

  // QuotaPriority 仅支持 ANTIGRAVITY 和 GEMINI_ACCOUNT
  if (platform === 'ANTIGRAVITY' || platform === 'GEMINI_ACCOUNT') {
    baseStrategies.push({
      value: GroupSchedulingStrategy.QuotaPriority,
      label: '配额优先'
    });
  }

  return baseStrategies;
}

// 平台切换时重置不兼容的策略
onPlatformChange(platform: string) {
  const currentStrategy = this.form.get('schedulingStrategy')?.value;

  if (currentStrategy === GroupSchedulingStrategy.QuotaPriority &&
      platform !== 'ANTIGRAVITY' && platform !== 'GEMINI_ACCOUNT') {
    // 重置为默认策略
    this.form.patchValue({
      schedulingStrategy: GroupSchedulingStrategy.LeastRequests
    });
  }
}
```

**文件**：`backend/src/AiRelay.Application/ProviderGroups/AppServices/ProviderGroupAppService.cs`

```csharp
// 后端验证策略兼容性
public async Task<ProviderGroupDto> CreateAsync(CreateProviderGroupDto input)
{
    // 验证策略兼容性
    if (input.SchedulingStrategy == GroupSchedulingStrategy.QuotaPriority)
    {
        var supportedPlatforms = new[] { ProviderPlatform.ANTIGRAVITY, ProviderPlatform.GEMINI_ACCOUNT };
        var groupPlatforms = input.AccountRelations.Select(r => r.Platform).Distinct();

        if (!groupPlatforms.All(p => supportedPlatforms.Contains(p)))
        {
            throw new BusinessException("QuotaPriority 策略仅支持 ANTIGRAVITY 和 GEMINI_ACCOUNT 平台");
        }
    }

    // ... 其他逻辑
}
```

---

## 4. 实施计划

### 4.1 阶段 1: 基础架构 (Day 1-2)

**任务清单**：

| 序号 | 任务 | 文件 | 验证方式 |
|------|------|------|------------|
| 1.1 | 实现 `IRateLimitTracker` 接口 | `IRateLimitTracker.cs` | 编译通过 |
| 1.2 | 实现 `RateLimitTracker` (Redis TTL + DB) | `RateLimitTracker.cs` | 单元测试 |
| 1.3 | 修改 `IAccountTokenAppService` 接口 | `IAccountTokenAppService.cs` | 编译通过 |
| 1.4 | 修改 `AccountTokenAppService.SelectAccountAsync` | `AccountTokenAppService.cs` | 单元测试 |

**验收标准**:
- [x] Redis 锁定和解锁功能正常
- [x] 数据库持久化状态一致性
- [x] 智能成功解锁机制生效

### 4.2 阶段 2: 中间件与路由架构 (Day 3-4)

**任务清单**:

| 序号 | 任务 | 文件 | 验证方式 |
|------|------|------|------------|
| 2.1 | 实现 `PlatformMetadata` | `PlatformMetadata.cs` | 编译通过 |
| 2.2 | 注册 `HttpForwarder` 与 `HttpMessageInvoker` | `Program.cs` | 服务启动正常 |
| 2.3 | 配置显式路由映射 | `Program.cs` | 路由请求命中中间件 |
| 2.4 | 实现 `IProxyPlatformStrategy` 及其基类 | `IProxyPlatformStrategy.cs` | 编译通过 |

**验收标准**:
- [x] 请求能正确路由到中间件并识别平台
- [x] YARP 配置文件配置移除不影响服务启动

### 4.3 阶段 3: 策略迁移与中间件实现 (Day 5-6)

**任务清单**:

| 序号 | 任务 | 文件 | 验证方式 |
|------|------|------|------------|
| 3.1 | 迁移 Antigravity Transform 逻辑 | `AntigravityProxyStrategy.cs` | 单元测试 |
| 3.2 | 实现 Gemini/Claude/OpenAI 策略 | `*ProxyStrategy.cs` | 单元测试 |
| 3.3 | 实现 `SmartReverseProxyMiddleware` 核心逻辑 | `SmartReverseProxyMiddleware.cs` | 集成测试 |
| 3.4 | 实现 `ExtractRetryAfter` 与退避算法 | `SmartReverseProxyMiddleware.cs` | 单元测试 |

**验收标准**:
- [x] 动态 BaseUrl 切换功能生效
- [x] 429/503 触发重试，自动切换账号
- [x] 最多重试 3 次
- [x] 无可用账号时返回错误

### 4.4 阶段 4: 配额管理 (Day 7)

**任务清单**:

| 序号 | 任务 | 文件 | 验证方式 |
|------|------|------|------------|
| 4.1 | 创建配额 DTO 与扩展 Client 接口 | `AccountQuotaInfo.cs`, `IChatModelClient.cs` | 编译通过 |
| 4.2 | 实现配额获取逻辑 | 各 `Client` 实现类 | API 测试 |
| 4.3 | 实现后台刷新服务 | `AccountQuotaRefreshBackgroundService.cs` | 服务运行测试 |
| 4.4 | 实现 `QuotaPriorityStrategy` | `QuotaPriorityStrategy.cs` | 单元测试 |

**验收标准**:
- [x] 后台服务定时刷新配额并缓存
- [x] 策略优先选择高配额账号

### 4.5 阶段 5: 前端实现 (Day 8)

**任务清单**:

| 序号 | 任务 | 文件 | 验证方式 |
|------|------|------|------------|
| 5.1 | 更新前端枚举 | `provider-group.dto.ts` | 编译通过 |
| 5.2 | 实现动态策略选项 | `group-edit-dialog.ts` | UI 测试 |
| 5.3 | 后端策略兼容性验证 | `ProviderGroupAppService.cs` | API 测试 |

**验收标准**:
- [x] ANTIGRAVITY/GEMINI_ACCOUNT 显示 QuotaPriority 选项
- [x] 其他平台不显示 QuotaPriority 选项
- [x] 平台切换时自动重置不兼容的策略

---

## 5. 关键变更清单

### 5.1 新增文件 (12 个)

| 文件 | 描述 |
|------|------|
| `AiRelay.Domain/Shared/Scheduling/IRateLimitTracker.cs` | 限流跟踪接口 |
| `AiRelay.Domain/Shared/Scheduling/RateLimitTracker.cs` | Redis TTL + 数据库持久化实现 |
| `AiRelay.Api/Middleware/SmartProxy/SmartReverseProxyMiddleware.cs` | 核心中间件 |
| `AiRelay.Api/Middleware/SmartProxy/PlatformMetadata.cs` | 路由元数据 |
| `AiRelay.Api/Middleware/SmartProxy/Strategies/IProxyPlatformStrategy.cs` | 策略接口 |
| `AiRelay.Api/Middleware/SmartProxy/Strategies/BaseProxyStrategy.cs` | 策略基类 |
| `AiRelay.Api/Middleware/SmartProxy/Strategies/*ProxyStrategy.cs` | 各平台策略实现 (4个) |
| `AiRelay.Api/BackgroundServices/AccountQuotaRefreshBackgroundService.cs` | 配额刷新后台服务 |
| `AiRelay.Domain/Shared/ExternalServices/ChatModel/Dto/AccountQuotaInfo.cs` | 配额 DTO |
| `AiRelay.Domain/ProviderGroups/DomainServices/QuotaPriorityStrategy.cs` | 配额优先策略 |

### 5.2 修改文件 (13 个)

| 文件 | 变更点 |
|------|--------|
| `Program.cs` | 移除 YARP 配置，添加 HttpMessageInvoker 和显式路由 |
| `IAccountTokenAppService.cs` | 添加 `excludeAccountIds` 参数 |
| `AccountTokenAppService.cs` | 传递 `excludeAccountIds` |
| `ProviderGroupDomainService.cs` | 添加 `excludeAccountIds` 参数 + 排除逻辑 |
| `IChatModelClient.cs` | 添加配额接口 |
| `AntigravityChatModelClient.cs` | 实现配额接口 |
| `GeminiAccountChatModelClient.cs` | 实现配额接口 |
| `ClaudeChatModelClient.cs` | 空实现 |
| `OpenAiChatModelClient.cs` | 空实现 |
| `GeminiApiChatModelClient.cs` | 空实现 |
| `GroupSchedulingStrategy.cs` | 添加 `QuotaPriority = 4` |
| `GroupSchedulingStrategyFactory.cs` | 注册 QuotaPriority |
| `appsettings.json` | 移除 ReverseProxy 配置 |

---

