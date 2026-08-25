# 当前用户套餐余量接口文档

## 1. 接口概述

该接口用于客户端获取当前登录用户的套餐、标准 Token 余量、周期用量和可展示模型信息。

设计参考现有接口：

```http
GET /api/v1/api-keys/default/default-models
```

参考文件：

```text
backend/src/AiRelay.Api/Controllers/ApiKeyController.cs
backend/src/AiRelay.Application/ApiKeys/AppServices/IApiKeyAppService.cs
backend/src/AiRelay.Application/ApiKeys/AppServices/ApiKeyAppService.cs
backend/src/AiRelay.Application/ApiKeys/Dtos/DefaultProviderModelsOutputDto.cs
```

本接口保持相同风格：

- 需要登录认证。
- 无请求体。
- Controller 只负责 HTTP 路由和调用 AppService。
- 当前用户由 AppService 通过 `currentUser` 获取。
- 返回强类型 OutputDto。
- 不使用 `ActionResult<T>` 包装。

---

## 2. 请求信息

### Method

```http
GET
```

### Path

```http
/api/v1/billing/usage/current
```

### Authorization

需要登录认证。

```http
Authorization: Bearer {access_token}
```

### Request Body

无。

### Query Parameters

无。

---

## 3. 响应 DTO

### CurrentBillingUsageOutputDto

```csharp
namespace AiRelay.Application.Billing.Dtos;

public class CurrentBillingUsageOutputDto
{
    /// <summary>
    /// 当前套餐编码：free / pro
    /// </summary>
    public required string PlanCode { get; init; }

    /// <summary>
    /// 当前套餐名称
    /// </summary>
    public required string PlanName { get; init; }

    /// <summary>
    /// 月费，Free 为空或 0，Pro 为 50
    /// </summary>
    public decimal? MonthlyPriceCny { get; init; }

    /// <summary>
    /// 标准 Token 折算规则
    /// </summary>
    public required StandardTokenRuleOutputDto StandardTokenRule { get; init; }

    /// <summary>
    /// 当前周期用量
    /// </summary>
    public required IReadOnlyList<BillingUsagePeriodOutputDto> Periods { get; init; }

    /// <summary>
    /// 页面展示的可访问模型
    /// </summary>
    public required IReadOnlyList<string> DisplayModels { get; init; }

    /// <summary>
    /// 当前是否已超出任一生效周期额度
    /// </summary>
    public bool IsExceeded { get; init; }

    /// <summary>
    /// 最近一次额度重置时间
    /// </summary>
    public DateTime NextResetTime { get; init; }
}
```

### StandardTokenRuleOutputDto

```csharp
namespace AiRelay.Application.Billing.Dtos;

public class StandardTokenRuleOutputDto
{
    /// <summary>
    /// 每 100 万标准 Tokens 对应的人民币金额
    /// </summary>
    public decimal CnyPerMillionStandardTokens { get; init; }

    /// <summary>
    /// USD 到 CNY 的后台配置汇率
    /// </summary>
    public decimal UsdToCnyRate { get; init; }
}
```

### BillingUsagePeriodOutputDto

```csharp
namespace AiRelay.Application.Billing.Dtos;

public class BillingUsagePeriodOutputDto
{
    /// <summary>
    /// 周期类型：day / week / month
    /// </summary>
    public required string PeriodType { get; init; }

    /// <summary>
    /// 周期开始时间
    /// </summary>
    public DateTime PeriodStart { get; init; }

    /// <summary>
    /// 周期结束时间
    /// </summary>
    public DateTime PeriodEnd { get; init; }

    /// <summary>
    /// 周期额度上限，单位：标准 Tokens
    /// </summary>
    public long LimitStandardTokens { get; init; }

    /// <summary>
    /// 周期已用量，单位：标准 Tokens
    /// </summary>
    public long UsedStandardTokens { get; init; }

    /// <summary>
    /// 周期剩余量，单位：标准 Tokens
    /// </summary>
    public long RemainingStandardTokens { get; init; }

    /// <summary>
    /// 使用百分比，范围 0-100
    /// </summary>
    public decimal UsagePercent { get; init; }

    /// <summary>
    /// 当前周期是否已超限
    /// </summary>
    public bool IsExceeded { get; init; }
}
```

---

## 4. 响应示例

### 4.1 Free 用户

```json
{
  "planCode": "free",
  "planName": "Free",
  "monthlyPriceCny": 0,
  "standardTokenRule": {
    "cnyPerMillionStandardTokens": 1,
    "usdToCnyRate": 7.2
  },
  "periods": [
    {
      "periodType": "month",
      "periodStart": "2026-05-01T00:00:00+08:00",
      "periodEnd": "2026-06-01T00:00:00+08:00",
      "limitStandardTokens": 30000000,
      "usedStandardTokens": 8500000,
      "remainingStandardTokens": 21500000,
      "usagePercent": 28.33,
      "isExceeded": false
    }
  ],
  "displayModels": ["qwen", "glm", "kimi", "minimax"],
  "isExceeded": false,
  "nextResetTime": "2026-06-01T00:00:00+08:00"
}
```

### 4.2 Pro 用户

```json
{
  "planCode": "pro",
  "planName": "Pro",
  "monthlyPriceCny": 50,
  "standardTokenRule": {
    "cnyPerMillionStandardTokens": 1,
    "usdToCnyRate": 7.2
  },
  "periods": [
    {
      "periodType": "day",
      "periodStart": "2026-05-17T00:00:00+08:00",
      "periodEnd": "2026-05-18T00:00:00+08:00",
      "limitStandardTokens": 720000000,
      "usedStandardTokens": 120000000,
      "remainingStandardTokens": 600000000,
      "usagePercent": 16.67,
      "isExceeded": false
    },
    {
      "periodType": "week",
      "periodStart": "2026-05-11T00:00:00+08:00",
      "periodEnd": "2026-05-18T00:00:00+08:00",
      "limitStandardTokens": 3600000000,
      "usedStandardTokens": 830000000,
      "remainingStandardTokens": 2770000000,
      "usagePercent": 23.06,
      "isExceeded": false
    },
    {
      "periodType": "month",
      "periodStart": "2026-05-01T00:00:00+08:00",
      "periodEnd": "2026-06-01T00:00:00+08:00",
      "limitStandardTokens": 7200000000,
      "usedStandardTokens": 2450000000,
      "remainingStandardTokens": 4750000000,
      "usagePercent": 34.03,
      "isExceeded": false
    }
  ],
  "displayModels": ["qwen", "glm", "kimi", "minimax"],
  "isExceeded": false,
  "nextResetTime": "2026-05-18T00:00:00+08:00"
}
```

---

## 5. 后端设计

### Controller

文件：

```text
backend/src/AiRelay.Api/Controllers/BillingUsageController.cs
```

代码：

```csharp
namespace AiRelay.Api.Controllers;

[Authorize]
[Route("api/v1/billing/usage")]
public class BillingUsageController(IBillingUsageAppService billingUsageAppService) : BaseController
{
    /// <summary>
    /// 获取当前用户套餐余量
    /// </summary>
    [HttpGet("current")]
    public async Task<CurrentBillingUsageOutputDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        return await billingUsageAppService.GetCurrentAsync(cancellationToken);
    }
}
```

### AppService 接口

文件：

```text
backend/src/AiRelay.Application/Billing/AppServices/IBillingUsageAppService.cs
```

代码：

```csharp
namespace AiRelay.Application.Billing.AppServices;

public interface IBillingUsageAppService : IAppService
{
    /// <summary>
    /// 获取当前用户套餐余量
    /// </summary>
    Task<CurrentBillingUsageOutputDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 累计使用记录费用到用户周期快照
    /// </summary>
    Task AccumulateUsageAsync(Guid usageRecordId, CancellationToken cancellationToken = default);
}
```

### AppService 实现职责

文件：

```text
backend/src/AiRelay.Application/Billing/AppServices/BillingUsageAppService.cs
```

职责：

```text
1. 从 currentUser 获取当前用户 ID。
2. 查询当前用户订阅；没有订阅则按 Free 处理。
3. 从 BillingPlanOptions 获取套餐额度。
4. 查询 UserBillingUsageSnapshot。
5. 缺失 Snapshot 时返回 0 已用，而不是创建脏数据。
6. 计算 Used / Remaining / UsagePercent / IsExceeded。
7. 返回 CurrentBillingUsageOutputDto。
```

---

## 6. 状态码

| 状态码 | 场景 |
|---|---|
| 200 | 查询成功 |
| 401 | 未登录或 Token 无效 |
| 403 | 用户订阅状态异常或被禁用 |
| 500 | 服务端异常 |

---

## 7. 前端使用建议

前端 service：

```text
frontend/src/app/features/platform/services/billing-usage-service.ts
```

前端 DTO：

```text
frontend/src/app/features/platform/models/billing-usage.dto.ts
```

Mock：

```text
frontend/_mock/api/billing-usage-api.ts
frontend/_mock/data/billing-usage-data.ts
```

调用时机：

- 控制台首页展示套餐余量。
- API Key 页面展示当前用户剩余额度。
- 请求失败 429 后刷新余量。

---

## 8. 验证用例

### Free 未超限

```text
Given 当前用户是 Free
And 月度已用 1,000 万标准 Tokens
When 调用 GET /api/v1/billing/usage/current
Then 返回 month.remainingStandardTokens = 2,000 万
And isExceeded = false
```

### Free 已超限

```text
Given 当前用户是 Free
And 月度已用 3,000 万标准 Tokens
When 调用 GET /api/v1/billing/usage/current
Then 返回 month.remainingStandardTokens = 0
And isExceeded = true
```

### Pro 周期混合

```text
Given 当前用户是 Pro
When 调用 GET /api/v1/billing/usage/current
Then 返回 day/week/month 三个周期
And 任一周期 isExceeded = true 时，根级 isExceeded = true
```

### 多 API Key 共享用户额度

```text
Given 用户有 API Key A 和 API Key B
And A 消耗 500 万标准 Tokens
And B 消耗 700 万标准 Tokens
When 查询当前用户余量
Then usedStandardTokens 应包含 A+B 共 1,200 万标准 Tokens
```
