# 渠道账户模型限流调度设计方案

> 版本：v1.1 | 日期：2026-04-17 | 状态：待审查

---

## 一、目标

当前系统的限流熔断是账户级的。

这会导致一个问题：当上游账户只是某个模型触发限流时，我们会把整个账户一起熔断，误伤同账户下仍可用的其它模型。

本方案目标：

1. 保留现有账户级熔断能力
2. 新增“按模型限流”模式，避免一个模型限流拖垮整个账户
3. 前端 `渠道账户` 页面以最小改动体现这种状态
4. 先完成前端与 mock，后补齐后端真实调度

---

## 二、现状结论

## 2.1 后端

当前关键链路：

1. `SmartReverseProxyMiddleware.InvokeAsync`
2. `SmartProxyAppService.SelectAccountAsync`
3. `ProviderGroupDomainService.SelectAccountForApiKeyAsync`
4. `AccountRateLimitDomainService`
5. `AccountResultHandlerDomainService`

当前特点：

1. 选号是账户维度
2. 限流判断是账户维度
3. 失败处理也是账户维度
4. 一旦 429/可重试错误触发熔断，整个账户会被标记为 `RateLimited`

这适合“整个账号共享额度”的场景，不适合“每个模型额度独立”的场景。

## 2.2 前端

当前 `渠道账户` 页面只能表达：

1. 账户正常
2. 账户限流
3. 账户异常

也就是说，前端目前只能表达“整个账户受限”，不能表达“账户可用，但部分模型受限”。

---

## 三、推荐方案

## 3.1 不引入 `QuotaExhausted`

本次不建议新增 `QuotaExhausted`。

原因：

1. 我们并不维护上游配额台账
2. 我们的核心策略是根据运行时错误做指数退避熔断
3. 上游额度可能是 1 天、1 周、1 个月，不值得在本系统里再做一套配额语义分类

建议统一抽象为“限流中”。

即使恢复时间有一定误差，也是可接受的，但建议误差偏保守，而不是偏激进：

1. 偏保守：恢复稍慢，损失一点吞吐，但稳定
2. 偏激进：恢复过早，可能持续撞上游限流，导致抖动

结论：

1. 保留当前指数退避自动适配周期的思路
2. 不新增 `QuotaExhausted`

## 3.2 不做复杂模型规则配置

本次不建议在账户上维护复杂的：

1. 哪些模型需要单独限流管理
2. 模型识别规则
3. 模型展示规则

这类设计过重，和当前系统的“运行时反馈熔断”模式不匹配。

建议直接收敛为一个简单字段：

```text
rateLimitScope = account | model
```

语义：

1. `account`
   任一限流错误作用于整个账户，保持当前行为
2. `model`
   若错误可明确归因到当前模型，则只锁定该模型；其它模型仍可调度

这是本次最合适的简化方案。

## 3.3 增加账户状态 `PartiallyRateLimited`

建议账户状态调整为：

1. `Normal`
2. `RateLimited`
3. `PartiallyRateLimited`
4. `Error`

语义：

1. `Normal`
   没有账户级限流，也没有模型级限流
2. `RateLimited`
   整个账户被限流
3. `PartiallyRateLimited`
   账户整体仍可用，但存在一个或多个模型受限
4. `Error`
   账户整体异常

这样前端就能准确表达“不是整个账户坏了，只是部分模型暂时不可用”。

---

## 四、后端设计

## 4.1 限流模式

建议给账户增加一个字段：

```csharp
public enum RateLimitScope
{
    Account = 0,
    Model = 1
}
```

`AccountToken` 增加：

```csharp
public RateLimitScope RateLimitScope { get; private set; }
```

默认值建议为：

```text
Account
```

这样对现有账户没有行为变化。

## 4.2 Redis Key 设计

保留现有账户级 key：

```text
RateLimit:{accountId}
account:backoff:{accountId}
```

新增模型级 key：

```text
RateLimit:Model:{accountId}:{modelKey}
account:model-backoff:{accountId}:{modelKey}
```

其中 `modelKey` 必须使用最终上游模型，即：

```text
MappedModelId
```

不能直接使用下游请求模型，否则会和 `modelMapping` 冲突。

## 4.3 调度判断位置

模型级限流判断不建议放进 `SelectAccountAsync` 或 `ProviderGroupDomainService.SelectAccountForApiKeyAsync`。

原因：

1. 这两个阶段还拿不到当前账户最终映射后的真实上游模型
2. 当前系统存在 `modelMapping`
3. 同一个下游模型在不同账户上可能映射到不同上游模型

因此，模型级限流判断最合适的位置是：

`SmartReverseProxyMiddleware.InvokeAsync`

流程建议：

1. `SelectAccountAsync` 先选出一个可用账号
2. 构建 `accountedHandler`
3. 调用 `ProcessRequestContextAsync(...)`
4. 获取 `upContext.MappedModelId`
5. 检查该账户在该模型上的模型级限流状态
6. 如果该模型受限，则切换账号，但不要把整个账户排除掉

示例伪代码：

```csharp
var upContext = await accountedHandler.ProcessRequestContextAsync(
    downContext,
    degradationLevel,
    context.RequestAborted);

var modelKey = upContext.MappedModelId ?? downContext.ModelId ?? "unknown";

if (await smartProxyAppService.IsModelRateLimitedAsync(
        selectResult.AccountToken.Id,
        modelKey,
        context.RequestAborted))
{
    shouldSwitchAccount = true;
    attemptStatusDesc = $"模型 {modelKey} 在账号 {selectResult.AccountToken.Name} 上已被限流";
    continue;
}
```

## 4.4 失败处理策略

关键原则：

1. `rateLimitScope = account` 时，保持当前行为
2. `rateLimitScope = model` 时：
   1. 若错误能明确归因到当前模型，则只锁模型
   2. 若错误无法明确归因，则仍按账户级处理

这里不要把 `model` 模式理解为“所有错误都只锁模型”。

否则会漏掉真正的账户级封禁或账号级异常。

建议扩展错误分析结果：

```csharp
public enum FailureScope
{
    Unknown = 0,
    Account = 1,
    Model = 2
}
```

并在失败处理时分流：

```csharp
if (account.RateLimitScope == RateLimitScope.Model
    && retryPolicy.FailureScope == FailureScope.Model
    && !string.IsNullOrWhiteSpace(modelKey))
{
    await smartProxyAppService.HandleModelFailureAsync(
        selectResult.AccountToken.Id,
        modelKey,
        httpStatusCode!.Value,
        proxyResponse.ErrorBody,
        retryPolicy,
        context.RequestAborted);
}
else
{
    await smartProxyAppService.HandleFailureAsync(
        new HandleFailureInputDto(
            selectResult.AccountToken.Id,
            httpStatusCode!.Value,
            proxyResponse.ErrorBody,
            retryPolicy),
        context.RequestAborted);
}
```

## 4.5 成功恢复策略

建议：

1. 账户级成功，继续沿用现有 `HandleSuccessAsync(accountId)`
2. 模型级成功，不主动清除模型锁，只清理模型级 backoff 计数
3. 模型锁仍然依赖 TTL 自然恢复

这样更稳，不容易过早解锁。

---

## 五、前端设计

## 5.1 编辑能力

`渠道账户` 编辑弹窗新增一个简单配置项：

1. `限流控制方式`
2. 可选值：
   1. `按账户`
   2. `按模型`

不增加复杂模型规则配置。

## 5.2 列表展示

在当前“调度状态”列中：

1. 保留原有账户状态标签
2. 新增 `部分限流` 状态展示
3. 若当前存在受限模型，则在状态旁显示 `+N`

例如：

1. `部分限流 +2`
2. `部分限流 +5`

其中 `N` 表示当前受限模型数。

## 5.3 `+N` 面板交互

建议复用当前“所属分组”列中 `+` 的交互方式。

点击 `+N` 后，弹出轻量面板，展示：

1. 模型名
2. 剩余解封时间
3. 原因摘要

例如：

1. `gemini-2.5-pro | 6小时后解除 | 上游返回 429`
2. `claude-sonnet-4 | 18小时后解除 | 触发指数退避`

这样用户能快速看到：

1. 哪些模型被限制
2. 什么时候可能恢复
3. 为什么被限制

## 5.4 详情弹窗

详情弹窗只做轻量补充：

1. 限流控制方式
2. 当前受限模型列表

不增加复杂配置区。

---

## 六、接口与 DTO 调整建议

## 6.1 前端 DTO

建议在 `frontend/src/app/features/platform/models/account-token.dto.ts` 中增加：

```ts
export enum RateLimitScope {
  Account = 'Account',
  Model = 'Model'
}

export interface LimitedModelStateDto {
  modelKey: string;
  lockedUntil?: string;
  statusDescription?: string;
}
```

并扩展账户 DTO：

```ts
rateLimitScope: RateLimitScope;
limitedModels?: LimitedModelStateDto[];
limitedModelCount?: number;
```

账户状态枚举增加：

```ts
PartiallyRateLimited = 'PartiallyRateLimited'
```

## 6.2 后端 DTO

建议在以下 DTO 上增加 `RateLimitScope`：

1. `AccountTokenOutputDto`
2. `CreateAccountTokenInputDto`
3. `UpdateAccountTokenInputDto`
4. `AvailableAccountTokenOutputDto`

并在输出 DTO 中增加：

1. `LimitedModels`
2. `LimitedModelCount`

列表页可以只返回摘要：

1. `LimitedModelCount`
2. 前几个 `LimitedModels`

详情页再返回完整列表。

---

## 七、目录结构调整建议

## 7.1 第一阶段：前端与 Mock

```text
frontend/
├── _mock/
│   ├── api/
│   │   └── account-token.ts
│   └── data/
│       └── account-token.ts
│
└── src/app/features/platform/
    ├── models/
    │   └── account-token.dto.ts
    ├── services/
    │   └── account-token-service.ts
    └── components/account-token/
        └── widgets/
            ├── account-table/
            │   ├── account-table.ts
            │   └── account-table.html
            ├── account-detail-dialog/
            │   ├── account-detail-dialog.ts
            │   └── account-detail-dialog.html
            └── account-edit-dialog/
                ├── account-edit-dialog.ts
                └── account-edit-dialog.html
```

第一阶段内容：

1. 增加 `rateLimitScope`
2. 增加 `PartiallyRateLimited`
3. mock 数据中增加 `limitedModels`
4. 列表页支持 `部分限流 +N`
5. `+N` 弹层展示受限模型
6. 编辑页支持切换“按账户 / 按模型”

## 7.2 第二阶段：后端补齐

```text
backend/src/
├── AiRelay.Domain/
│   └── ProviderAccounts/
│       ├── DomainServices/
│       │   ├── AccountRateLimitDomainService.cs
│       │   ├── AccountModelRateLimitDomainService.cs
│       │   └── AccountResultHandlerDomainService.cs
│       ├── Entities/
│       │   └── AccountToken.cs
│       └── ValueObjects/
│           ├── RateLimitScope.cs
│           └── FailureScope.cs
│
├── AiRelay.Application/
│   ├── ProviderAccounts/
│   │   ├── Dtos/
│   │   └── AppServices/
│   └── ProviderGroups/
│       └── AppServices/
│
├── AiRelay.Infrastructure/
│   └── Persistence/
│       ├── EntityConfigurations/
│       └── Migrations/
│
└── AiRelay.Api/
    └── Middleware/SmartProxy/
        └── SmartReverseProxyMiddleware.cs
```

第二阶段内容：

1. 账户新增 `RateLimitScope`
2. 新增模型级 Redis 锁与 backoff
3. 中间件按 `MappedModelId` 判断模型级限流
4. 失败处理按 `FailureScope` 分流到账户级或模型级
5. 查询接口聚合返回 `limitedModels`

---

## 八、分两阶段实施计划

## 8.1 第一阶段：前端功能及 `frontend/_mock`

目标：

先把管理端能力和接口契约跑通，不依赖真实后端。

实施内容：

1. 前端 DTO 扩展 `RateLimitScope`、`PartiallyRateLimited`、`limitedModels`
2. `frontend/_mock/data/account-token.ts` 增加样例数据
3. `frontend/_mock/api/account-token.ts` 支持新字段读写
4. `渠道账户` 列表展示 `部分限流 +N`
5. `+N` 弹层展示受限模型
6. 编辑弹窗增加“按账户 / 按模型”开关
7. 详情弹窗展示限流模式和受限模型列表

验收标准：

1. 前端能准确区分“整个账户限流”和“部分模型限流”
2. 页面交互完整跑通
3. mock 契约与后端预期一致

## 8.2 第二阶段：后端功能补齐

目标：

让真实调度链路支持模型级限流。

实施内容：

1. 持久化账户字段 `RateLimitScope`
2. 新增模型级限流领域服务
3. 增加模型级 Redis 锁与 backoff
4. 在 `SmartReverseProxyMiddleware` 中基于 `MappedModelId` 检查模型限流
5. 将失败处理按 `FailureScope` 分为账户级和模型级
6. 对外接口返回 `limitedModels` 与 `limitedModelCount`

验收标准：

1. 某模型被限流时，同账户其它模型仍可调度
2. 账户级故障仍保持现有保护能力
3. 前端展示与真实调度状态一致

---

## 九、最终建议

结合当前代码结构和你的目标，本次最优方案是：

1. 不增加 `QuotaExhausted`
2. 不增加复杂模型规则配置
3. 账户只增加一个配置：`按账户限流` / `按模型限流`
4. 账户状态增加 `PartiallyRateLimited`
5. 前端列表用 `+N` 面板展示当前受限模型
6. 后端基于 `accountId + MappedModelId` 做模型级熔断
7. 模型级恢复继续依赖现有的指数退避 + TTL 自动恢复机制

这套方案改动小、表达清晰，并且能真正解决“一个模型限流导致整个账户被误伤”的核心问题。
