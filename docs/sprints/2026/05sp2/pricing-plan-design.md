# AiRelay 标准 Token 套餐、支付与额度控制设计方案

## 1. 目标与范围

本方案用于定义 AiRelay 当前阶段的 Free / Pro 套餐、Landing 展示、支付宝购买 Pro、客户端余量接口和后端额度控制闭环。

目标：

- 使用“标准 Tokens”作为套餐展示和额度控制口径。
- Free 默认每月 `3,000 万标准 Tokens`，每月重置不结转。
- Pro 订阅价格 `50 元 / 月`，提供日 / 周 / 月调用上限。
- 当前阶段不做模型差异化，仅展示可访问模型：`qwen`、`glm`、`kimi`、`minimax`。
- Pro 购买以支付宝支付为例，用户点击购买后跳转支付宝，支付完成后套餐生效。
- 后端以 `UsageRecord.FinalCost` 为最终成本来源，折算为标准 Tokens 后进入用户级额度统计。
- 提供客户端接口查询当前登录用户套餐、余量和周期用量。

当前不做：

- 不展示 GPT / Gemini。
- 不做 Free / Pro 模型族权限差异。
- 不实现退款、自动续费、优惠券、发票等完整支付中台能力。
- 不实现后台套餐配置页面。
- 不按单模型展示复杂价格表。

---

## 2. 已采纳策略结论

- 标准 Token 固定为：`1 元人民币等值模型用量 = 100 万标准 Tokens`。
- 汇率第一阶段使用后端 Options 默认值：`UsdToCnyRate = 7.2`。
- Free 月度重置周期按北京时间自然月处理。
- Pro 第一阶段只保留月付：`50 元 / 月`，年付折扣后续再设计。
- `XxxOptions` 提供默认值，第一阶段不在 `appsettings.json` 中显式展示配置。

---

## 3. 最终套餐策略

### 3.1 标准 Token 口径

```text
1 元人民币等值模型用量 = 100 万标准 Tokens
```

折算公式：

```text
标准 Tokens = 最终扣费金额 CNY × 1,000,000
```

如果 `UsageRecord.FinalCost` 当前以 USD 计价，则使用默认汇率折算：

```text
最终扣费金额 CNY = UsageRecord.FinalCost × UsdToCnyRate
标准 Tokens = UsageRecord.FinalCost × UsdToCnyRate × 1,000,000
```

### 3.2 Free 套餐

- 价格：免费
- 默认额度：`3,000 万标准 Tokens / 月`
- 等值成本：约 `30 元人民币` 模型用量
- 每月按北京时间自然月重置
- 不结转
- 页面展示模型：`qwen`、`glm`、`kimi`、`minimax`

### 3.3 Pro 套餐

- 订阅价格：`50 元 / 月`
- 每日上限：`7.2 亿标准 Tokens`
- 每周上限：`36 亿标准 Tokens`
- 每月上限：`72 亿标准 Tokens`
- 页面展示模型：`qwen`、`glm`、`kimi`、`minimax`

> Pro 是调用上限，不是赠送余额。

---

## 4. Options 默认值设计

第一阶段通过 Options 类内置默认值承载套餐策略，不在 `appsettings.json` 中显式配置。后续如果需要后台配置或环境差异，再通过配置覆盖默认值。

```csharp
namespace AiRelay.Domain.Billing.Options;

public class BillingPlanOptions
{
    public StandardTokenOptions StandardToken { get; init; } = new();

    public IReadOnlyList<string> DisplayModels { get; init; } = ["qwen", "glm", "kimi", "minimax"];

    public BillingPlanDefinition Free { get; init; } = BillingPlanDefinition.FreeDefault();

    public BillingPlanDefinition Pro { get; init; } = BillingPlanDefinition.ProDefault();
}

public class StandardTokenOptions
{
    public decimal CnyPerMillionStandardTokens { get; init; } = 1m;

    public decimal UsdToCnyRate { get; init; } = 7.2m;
}

public class BillingPlanDefinition
{
    public required string Code { get; init; }

    public required string Name { get; init; }

    public decimal? MonthlyPriceCny { get; init; }

    public long? DailyLimitStandardTokens { get; init; }

    public long? WeeklyLimitStandardTokens { get; init; }

    public long MonthlyLimitStandardTokens { get; init; }

    public static BillingPlanDefinition FreeDefault()
    {
        return new BillingPlanDefinition
        {
            Code = "free",
            Name = "Free",
            MonthlyPriceCny = 0,
            MonthlyLimitStandardTokens = 30_000_000
        };
    }

    public static BillingPlanDefinition ProDefault()
    {
        return new BillingPlanDefinition
        {
            Code = "pro",
            Name = "Pro",
            MonthlyPriceCny = 50,
            DailyLimitStandardTokens = 720_000_000,
            WeeklyLimitStandardTokens = 3_600_000_000,
            MonthlyLimitStandardTokens = 7_200_000_000
        };
    }
}
```

---

## 5. 支付购买 Pro 设计

### 5.1 购买链路

```text
用户点击 Pro 购买
  -> 前端调用 CreateBillingOrder
  -> 后端创建待支付订单
  -> 后端生成支付宝支付跳转地址或表单
  -> 前端跳转支付宝收银台
  -> 用户完成支付
  -> 支付宝异步通知后端
  -> 后端验签、校验金额、幂等更新订单
  -> 创建或续期 UserSubscription
  -> 用户套餐生效
  -> 前端查询 CurrentBillingUsage 刷新套餐状态
```

### 5.2 前端交互

Pricing 卡片中 Pro 下方提供购买按钮：

```text
购买 Pro
```

点击后：

1. 检查用户是否已登录。
2. 未登录则跳转登录。
3. 已登录则调用创建订单接口。
4. 后端返回支付宝支付跳转地址或支付表单内容。
5. 前端跳转到支付宝支付界面。
6. 支付完成回到前端支付结果页。
7. 前端轮询订单状态或重新查询当前套餐余量。

推荐前端结果页：

```text
frontend/src/app/features/platform/pages/billing-payment-result/
```

### 5.3 后端订单接口

创建 Pro 订单：

```http
POST /api/v1/billing/orders/pro
```

请求体第一阶段可为空，套餐和金额由后端决定，不能信任前端传入价格。

返回 DTO：

```csharp
namespace AiRelay.Application.Billing.Dtos;

public class CreateBillingOrderOutputDto
{
    public required Guid OrderId { get; init; }

    public required string OrderNo { get; init; }

    public required string PlanCode { get; init; }

    public decimal AmountCny { get; init; }

    public required string PaymentProvider { get; init; }

    public required string PaymentUrl { get; init; }

    public DateTime ExpiresAt { get; init; }
}
```

查询订单状态：

```http
GET /api/v1/billing/orders/{id}
```

返回订单状态用于支付结果页轮询。

### 5.4 支付宝异步通知

支付宝异步通知接口建议独立放在支付回调路由下：

```http
POST /api/v1/payments/alipay/notify
```

设计要求：

- 使用 `[AllowAnonymous]`，因为支付宝服务器不会携带用户登录态。
- 必须校验支付宝签名。
- 必须校验 `app_id`、订单号、金额、币种、交易状态。
- 必须使用订单幂等状态机，防止重复通知重复开通套餐。
- 只有订单为 `Pending` 且支付宝交易成功时才更新为 `Paid`。
- 回调处理成功后按支付宝要求返回 `success`。

第一阶段只处理：

```text
TRADE_SUCCESS
TRADE_FINISHED
```

### 5.5 套餐生效规则

支付成功后创建或更新 `UserSubscription`：

- 用户当前是 Free：开通 Pro，有效期从支付成功时间开始，截止到下个月同一时间。
- 用户当前 Pro 已过期：从支付成功时间重新开通一个月。
- 用户当前 Pro 未过期：在当前 `EndTime` 基础上顺延一个月。
- 生效套餐为 `pro`。
- 订阅状态为 `Active`。

建议时间统一使用 UTC 存储，展示和自然月额度计算使用北京时间。

### 5.6 支付安全要求

- 金额只能由后端根据 `BillingPlanOptions.Pro.MonthlyPriceCny` 生成。
- 订单号必须全局唯一。
- 支付回调必须验签，不能只依赖前端 returnUrl。
- 前端支付成功页只用于展示，不作为套餐生效依据。
- 支付宝通知可能重复到达，订单更新必须幂等。
- 支付成功与套餐生效建议在同一个事务中完成。

---

## 6. 后端额度控制闭环

### 6.1 总体链路

```text
BillingPlanOptions 默认套餐配置
  -> BillingOrder 支付订单
  -> UserSubscription 用户订阅
  -> UserBillingUsageSnapshot 用户周期用量快照
  -> GET /api/v1/billing/usage/current 客户端查询
  -> SmartReverseProxyMiddleware 请求前额度校验
  -> ModelRouteAppService 执行路由
  -> UsageLifecycleAppService.FinishUsageAsync 计算 UsageRecord.FinalCost
  -> 折算标准 Tokens
  -> 累计 UserBillingUsageSnapshot
```

### 6.2 请求前额度校验

控制点放在 `SmartReverseProxyMiddleware.InvokeAsync` 中，位于请求解析之后、路由候选解析之前：

```text
ValidateAndGetContext
  -> ProcessDownstreamRequestAsync
  -> BillingAccessAppService.ValidateProxyRequestAsync
  -> ResolveProxyRouteCandidatesAsync
  -> ExecuteRouteAsync
```

第一版校验规则：

- 没有订阅时按 Free 处理。
- 查询当前用户生效周期快照。
- Free 校验月度额度。
- Pro 同时校验日 / 周 / 月额度。
- 任一生效周期已超限则拒绝。

拒绝建议：

- 额度超限：`429 Too Many Requests`
- 订阅异常：`403 Forbidden`

### 6.3 完成后累计额度

在 `UsageLifecycleAppService.FinishUsageAsync` 中完成费用计算后，使用 `UsageRecord.FinalCost` 累计用户级标准 Token 用量。

建议新增：

```text
IBillingUsageAppService.AccumulateUsageAsync(Guid usageRecordId, CancellationToken cancellationToken = default)
```

累计规则：

1. 读取 `UsageRecord.FinalCost`。
2. 使用 `UsdToCnyRate` 折算人民币成本。
3. 折算为标准 Tokens。
4. 按用户维度更新日 / 周 / 月 `UserBillingUsageSnapshot`。
5. 保留现有 ApiKey / AccountToken 统计逻辑不变。

---

## 7. 分层与命名规范

后端实现对齐现有 AccountToken 增删改查风格：

- Controller 位于 `AiRelay.Api.Controllers`。
- Controller 使用 `[Authorize]`、`[Route("api/v1/xxx")]`，继承 `BaseController`。
- Controller 只处理 HTTP 路由和调用 AppService，返回 DTO 本身，不使用 `ActionResult<T>` 包装。
- AppService 接口命名为 `I*AppService`，实现命名为 `*AppService`。
- DTO 位于 `AiRelay.Application.{Module}.Dtos`。
- 输入 DTO 使用 `Create*InputDto`、`Update*InputDto`、`Get*PagedInputDto`、`Validate*InputDto`。
- 输出 DTO 使用 `*OutputDto`。
- HTTP 入参 DTO 使用 `[Display]`、`[Required]`、`[MaxLength]`、`[Range]` 等 DataAnnotations。
- 实体属性保持 `private set`，通过业务方法修改状态。

客户端余量接口对齐现有：

```http
GET /api/v1/api-keys/default/default-models
```

支付回调接口例外：支付宝异步通知需要 `[AllowAnonymous]`，但必须通过签名校验和订单状态机保证安全。

---

## 8. 调整内容树形目录结构

### 8.1 第一阶段：Landing、mock 与支付入口

```text
frontend/
├── _mock/
│   ├── api/
│   │   ├── pricing-plan-api.ts
│   │   └── billing-order-api.ts
│   ├── data/
│   │   ├── pricing-plan-data.ts
│   │   └── billing-order-data.ts
│   └── index.ts
└── src/app/features/
    ├── public/
    │   ├── components/landing/
    │   │   ├── landing.ts
    │   │   └── landing.html
    │   ├── models/
    │   │   └── pricing-plan.dto.ts
    │   ├── services/
    │   │   └── pricing-plan-service.ts
    │   └── widgets/pricing/
    │       ├── pricing.ts
    │       └── pricing.html
    └── platform/
        ├── models/
        │   └── billing-order.dto.ts
        ├── services/
        │   └── billing-order-service.ts
        └── pages/billing-payment-result/
            ├── billing-payment-result.ts
            └── billing-payment-result.html
```

### 8.2 后端套餐、支付与额度闭环

```text
backend/src/
├── AiRelay.Api/
│   └── Controllers/
│       ├── BillingPlanController.cs
│       ├── BillingOrderController.cs
│       ├── BillingUsageController.cs
│       └── AlipayNotifyController.cs
├── AiRelay.Application/
│   └── Billing/
│       ├── AppServices/
│       │   ├── IBillingPlanAppService.cs
│       │   ├── BillingPlanAppService.cs
│       │   ├── IBillingOrderAppService.cs
│       │   ├── BillingOrderAppService.cs
│       │   ├── IBillingUsageAppService.cs
│       │   ├── BillingUsageAppService.cs
│       │   ├── IBillingAccessAppService.cs
│       │   └── BillingAccessAppService.cs
│       └── Dtos/
│           ├── BillingPlanOutputDto.cs
│           ├── BillingPlanLimitOutputDto.cs
│           ├── CreateBillingOrderOutputDto.cs
│           ├── BillingOrderOutputDto.cs
│           ├── CurrentBillingUsageOutputDto.cs
│           ├── BillingUsagePeriodOutputDto.cs
│           ├── StandardTokenRuleOutputDto.cs
│           └── ValidateProxyRequestInputDto.cs
├── AiRelay.Domain/
│   └── Billing/
│       ├── Entities/
│       │   ├── BillingOrder.cs
│       │   ├── PaymentTransaction.cs
│       │   ├── UserSubscription.cs
│       │   └── UserBillingUsageSnapshot.cs
│       ├── Enums/
│       │   ├── BillingOrderStatus.cs
│       │   ├── BillingPaymentProvider.cs
│       │   ├── BillingPlanCode.cs
│       │   ├── BillingPeriodType.cs
│       │   └── SubscriptionStatus.cs
│       ├── Options/
│       │   ├── BillingPlanOptions.cs
│       │   ├── BillingPlanDefinition.cs
│       │   ├── StandardTokenOptions.cs
│       │   └── AlipayOptions.cs
│       └── DomainServices/
│           ├── BillingOrderDomainService.cs
│           ├── BillingPlanDomainService.cs
│           └── BillingUsageDomainService.cs
├── AiRelay.Application.Contracts/
│   └── Payments/
│       └── IAlipayPaymentService.cs
└── AiRelay.Infrastructure/
    ├── EntityFrameworkCore/Configurations/
    │   ├── BillingOrderConfiguration.cs
    │   ├── PaymentTransactionConfiguration.cs
    │   ├── UserSubscriptionConfiguration.cs
    │   └── UserBillingUsageSnapshotConfiguration.cs
    └── Payments/
        └── AlipayPaymentService.cs
```

---

## 9. 客户端余量接口

新增接口：

```http
GET /api/v1/billing/usage/current
```

用途：客户端获取当前登录用户套餐、标准 Token 折算规则、当前周期用量、剩余额度和页面展示模型。

详细接口文档：

```text
docs/sprints/2026/05sp2/billing-usage-current-api.md
```

核心 Controller：

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

核心 AppService 接口：

```csharp
namespace AiRelay.Application.Billing.AppServices;

public interface IBillingUsageAppService : IAppService
{
    Task<CurrentBillingUsageOutputDto> GetCurrentAsync(CancellationToken cancellationToken = default);

    Task AccumulateUsageAsync(Guid usageRecordId, CancellationToken cancellationToken = default);
}
```

---

## 10. Landing 页面文案

### Free

```text
Free
每月 3,000 万标准 Tokens

适合国产模型体验、Prompt 调试、个人脚本和轻量 Agent。
额度每月自动重置，不结转。

包含：
- 每月 3,000 万标准 Tokens
- 可访问 Qwen / GLM / Kimi / MiniMax
- 按模型实际价格折算消耗
```

### Pro

```text
Pro
50 元 / 月

获得更高调用上限，适合生产业务、团队应用和高频 API 调用。

包含：
- 每日 7.2 亿标准 Tokens
- 每周 36 亿标准 Tokens
- 每月 72 亿标准 Tokens
- 可访问 Qwen / GLM / Kimi / MiniMax
- 支付后立即生效
```

按钮：

```text
购买 Pro
```

---

## 11. 实施计划

### 第一阶段：前端 Pricing 展示与 mock

1. 更新 Pricing mock 数据为标准 Token 策略。
2. 页面只展示可访问模型：`qwen/glm/kimi/minimax`。
3. Landing 接入 Pricing 区块。
4. Pro 卡片增加“购买 Pro”按钮。
5. 使用 PrimeNG Card / Button / Tag / Divider 展示套餐。
6. 使用 Tailwind CSS v4 完成响应式布局。
7. 浏览器验证桌面端、移动端和深浅色主题展示。

### 第二阶段：套餐、订单与支付宝支付

1. 新增带默认值的 `BillingPlanOptions`、`StandardTokenOptions`、`BillingPlanDefinition`。
2. 新增 `BillingOrder`、`PaymentTransaction`、`UserSubscription`。
3. 新增 `IBillingOrderAppService.CreateProOrderAsync`。
4. 接入支付宝下单能力，返回支付跳转地址或表单。
5. 新增支付宝异步通知接口并完成验签、金额校验、幂等更新。
6. 支付成功后创建或续期 Pro 订阅。
7. 前端支付结果页查询订单状态并刷新套餐余量。

### 第三阶段：后端套餐余量接口

1. 新增 `BillingUsageController.GetCurrentAsync`。
2. 新增 `IBillingUsageAppService.GetCurrentAsync`。
3. 新增 `CurrentBillingUsageOutputDto`、`BillingUsagePeriodOutputDto`、`StandardTokenRuleOutputDto`。
4. 前端接入 `GET /api/v1/billing/usage/current` 展示真实余量。

### 第四阶段：后端额度控制闭环

1. 新增 `UserBillingUsageSnapshot`。
2. 新增 `BillingAccessAppService`。
3. 在 `SmartReverseProxyMiddleware` 请求前校验套餐额度。
4. 在 `UsageRecord` 完成后累计用户标准 Token 用量。
5. 控制台展示当前周期已用量、剩余额度和超限状态。

---

## 12. 验证方式

前端：

```bash
npm run lint
npm run build
```

后端：

```bash
dotnet build
dotnet test
```

接口验证：

```http
GET /api/v1/billing/usage/current
POST /api/v1/billing/orders/pro
GET /api/v1/billing/orders/{id}
POST /api/v1/payments/alipay/notify
```

支付验证：

```text
未登录点击购买 Pro：跳转登录
已登录点击购买 Pro：创建待支付订单并跳转支付宝
支付宝支付成功：异步通知验签通过，订单变为 Paid
重复通知：不重复续期
金额不一致：拒绝更新订单
前端 returnUrl 成功但异步通知未到达：套餐不生效，结果页显示处理中
```

代理额度验证：

```text
Free 未超月度标准 Token：允许请求
Free 超过月度标准 Token：返回 429
Pro 支付成功且未超日 / 周 / 月标准 Token：允许请求
Pro 超过任一周期标准 Token：返回 429
多个 API Key 共享同一用户额度
```
