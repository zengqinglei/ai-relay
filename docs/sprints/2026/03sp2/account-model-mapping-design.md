# 渠道账户模型映射功能设计

## 目标

为每个渠道账户支持配置**自定义模型白名单**与**模型映射规则**，实现：
1. 选号时快速过滤不支持该模型的账户（高性能，纯内存）
2. 未配置时降级到平台默认可用模型列表（`IModelProvider.GetAvailableModels`）
3. 请求转发时按账户配置替换模型名称

---

## 影响目录结构

```
backend/src/
├── AiRelay.Domain/
│   └── ProviderAccounts/
│       └── DomainServices/
│           ├── AccountTokenDomainService.cs          ← 新增 IsModelSupportedAsync / GetModelMappingFromExtra
│           └── UpstreamModelCacheDomainService.cs    ← 新增（上游模型 ID 缓存，30-60min TTL）
│
├── AiRelay.Application/
│   └── ProviderAccounts/
│       ├── Dtos/
│       │   ├── AccountTokenOutputDto.cs              ← 新增 ModelMapping
│       │   ├── CreateAccountTokenInputDto.cs         ← 新增 ModelMapping
│       │   └── UpdateAccountTokenInputDto.cs         ← 新增 ModelMapping
│       ├── Mappings/
│       │   └── AccountTokenProfile.cs                ← 新增映射规则
│       └── AppServices/
│           └── AccountTokenAppService.cs             ← 调用 DomainService.GetAvailableModelsAsync
│
├── AiRelay.Infrastructure/
│   └── Shared/ExternalServices/ChatModel/
│       └── Processors/
│           ├── ModelMappingHelper.cs                 ← 新增（共享 ResolveMapping 逻辑）
│           ├── Antigravity/AntigravityModelIdMappingProcessor.cs  ← 改造（注入 Options）
│           ├── Claude/ClaudeModelIdMappingProcessor.cs            ← 改造（注入 Options）
│           ├── OpenAi/OpenAiModelIdMappingProcessor.cs            ← 改造（注入 Options）
│           └── Gemini/
│               ├── GeminiOAuthModelIdMappingProcessor.cs          ← 新增
│               └── GeminiApiKeyModelIdMappingProcessor.cs         ← 新增
│
frontend/src/app/
├── shared/constants/
│   └── model-mapping.constants.ts                   ← 新增
├── shared/components/model-mapping-editor/          ← 新增组件
└── features/platform/
    ├── models/
    │   └── account-token.dto.ts                     ← 新增 modelMapping
    └── components/account-token/widgets/
        └── account-edit-dialog/                     ← 改造 UI + 逻辑
```

---

## 存储设计

**不新增数据库字段**，复用 `AccountToken.ExtraProperties`，使用两个独立字段：

| 字段 | 类型 | 用途 |
|------|------|------|
| `model_whitelist` | `string[]` | 白名单模型列表（限制可接受的模型） |
| `model_mapping` | `Record<string, string>` | 模型映射规则（转换模型名称） |

**白名单示例**（仅限制，不转换）：
```json
{
  "model_whitelist": ["claude-sonnet-4-6", "claude-haiku-4-5-20251001", "custom-model-v1"]
}
```

**映射规则示例**（仅转换，不限制）：
```json
{
  "model_mapping": {
    "claude-opus-4-6": "claude-sonnet-4-5-20250929",
    "claude-ops-*": "claude-sonnet-*",
    "gemini-2.5-pro": "gemini-2.5-flash"
  }
}
```

**同时配置示例**（先限制再转换）：
```json
{
  "model_whitelist": ["claude-opus-4-6", "claude-sonnet-4-6"],
  "model_mapping": {
    "claude-opus-4-6": "claude-sonnet-4-6"
  }
}
```
> 处理顺序：先检查白名单（是否接受），再应用映射规则（如何转换）

---

## 通配符规则说明

`*` 仅支持出现在末尾（一个），分两种情形：

| key | value | 行为 |
|-----|-------|------|
| `claude-*` | `claude-sonnet-4-5` | 所有 `claude-` 前缀模型 → 固定目标 |
| `claude-ops-*` | `claude-sonnet-*` | 前缀替换：`claude-ops-flash` → `claude-sonnet-flash` |

**匹配优先级**：精确匹配 > 最长通配前缀匹配。

**ResolveMapping 核心逻辑**（供 Domain Service 和 Processor 共用）：

```csharp
// AccountTokenDomainService.cs — 静态辅助方法
private static string? ResolveMapping(string model, Dictionary<string, string> mapping)
{
    // 1. 精确匹配
    if (mapping.TryGetValue(model, out var exact))
        return exact;

    // 2. 通配符匹配（最长前缀优先）
    var match = mapping
        .Where(kv => kv.Key.EndsWith('*') && model.StartsWith(kv.Key[..^1]))
        .OrderByDescending(kv => kv.Key.Length)
        .FirstOrDefault();

    if (match.Key == null) return null;

    // 3. value 也以 * 结尾 → 前缀替换（保留 suffix）
    if (match.Value.EndsWith('*'))
    {
        var suffix = model[match.Key.Length - 1..]; // 去掉 key 前缀后的部分
        return match.Value[..^1] + suffix;
    }

    return match.Value;
}
```

---

## 核心流程

### 1. ExtraProperties 中 model_mapping 的读写

**不在 `AccountToken` 实体上添加方法**，遵循现有 ExtraProperties 使用惯例：
- 读取：由 `AccountTokenDomainService` 提供静态辅助方法
- 写入：由 `AccountTokenAppService` 在 Create/Update 时序列化后写入 `ExtraProperties`

```csharp
// AccountTokenDomainService.cs — 新增静态辅助方法（供 Processor 和 DomainService 共用）
internal static Dictionary<string, string>? GetModelMappingFromExtra(
    Dictionary<string, string> extraProperties)
{
    if (!extraProperties.TryGetValue("model_mapping", out var json))
        return null;
    return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
}
```

```csharp
// AccountTokenAppService.cs — Create/Update 时处理
if (input.ModelMapping != null && input.ModelMapping.Count > 0)
    extraProperties["model_mapping"] = JsonSerializer.Serialize(input.ModelMapping);
else
    extraProperties.Remove("model_mapping");
```

---

### 2. 核心抽象：账户模型ID缓存（UpstreamModelCacheDomainService）

三个入口的本质需求拆分：

| 入口 | 需要什么 | 是否允许 IO |
|------|---------|------------|
| 选号过滤（热路径） | 账户支持的模型ID集合（判断是否支持） | 否，纯内存 |
| 模型测试dialog（UI） | 账户可展示的模型列表（含Label） | 是，异步拉取 |
| XxxModelIdMappingProcessor | 模型ID转换规则 | 否，纯内存 |

**关键认知**：白名单和上游模型缓存是**并列的数据来源**：
- 有白名单配置 → 直接用白名单，**不需要**上游模型缓存
- 无白名单配置 → 用上游模型缓存（异步，UI用）或静态平台模型（同步，选号用）

因此，上游模型拉取+缓存应从 `AccountTokenDomainService` 进一步分离到专用的 **`UpstreamModelCacheDomainService`**，职责更清晰：

```csharp
// AiRelay.Domain/ProviderAccounts/DomainServices/UpstreamModelCacheDomainService.cs
/// <summary>
/// 上游模型列表缓存服务（30-60分钟 TTL）
/// 仅在无白名单配置时使用，供 UI 展示过滤
/// </summary>
public class UpstreamModelCacheDomainService(
    IDistributedCache cache,
    IChatModelHandlerFactory chatModelHandlerFactory,
    ILogger<UpstreamModelCacheDomainService> logger)
{
    private static readonly TimeSpan MinTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxTtl = TimeSpan.FromMinutes(60);

    /// <summary>
    /// 获取上游模型ID集合（缓存命中则直接返回，用于选号过滤降级路径）
    /// 注意：仅返回缓存，不触发网络请求，供选号循环中同步降级使用
    /// </summary>
    public async Task<HashSet<string>?> GetCachedModelIdsAsync(Guid accountId, CancellationToken ct = default)
    {
        var cached = await cache.GetStringAsync(CacheKey(accountId), ct);
        if (string.IsNullOrEmpty(cached)) return null;
        var models = JsonSerializer.Deserialize<List<string>>(cached);
        return models?.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 拉取上游模型列表并写入缓存（供 UI 展示调用，允许 IO）
    /// </summary>
    public async Task<IReadOnlyList<string>?> FetchAndCacheAsync(
        AccountToken account,
        CancellationToken ct = default)
    {
        try
        {
            var handler = chatModelHandlerFactory.CreateHandler(
                account.Platform, account.AccessToken!, account.BaseUrl,
                account.ExtraProperties, shouldMimicOfficialClient: true);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var upstreamModels = await handler.GetModelsAsync(cts.Token);
            if (upstreamModels == null || upstreamModels.Count == 0) return null;

            var ids = upstreamModels.Select(m => m.Value).ToList();

            // 随机 TTL 避免缓存雪崩
            var ttl = MinTtl + TimeSpan.FromMinutes(Random.Shared.NextDouble() * (MaxTtl - MinTtl).TotalMinutes);
            await cache.SetStringAsync(
                CacheKey(account.Id),
                JsonSerializer.Serialize(ids),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                ct);

            logger.LogInformation("上游模型缓存写入: AccountId={AccountId}, Count={Count}, TTL={TTL}min",
                account.Id, ids.Count, (int)ttl.TotalMinutes);
            return ids;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "上游模型拉取失败: AccountId={AccountId}", account.Id);
            return null;
        }
    }

    private static string CacheKey(Guid accountId) => $"account:upstream-models:{accountId}";
}
```

---

### 3. 选号过滤（高性能路径）

**位置**：`ProviderGroupDomainService.cs` — 现有过滤循环（L275-308）新增过滤5

**方法下沉到 `AccountTokenDomainService`**：

```csharp
// AccountTokenDomainService.cs — 新增方法（注入 IModelProvider + UpstreamModelCacheDomainService）
/// <summary>
/// 判断账户是否支持指定模型（选号热路径）
/// 优先级：白名单配置 > 上游模型缓存 > 平台静态模型（全通）
/// 全部为纯内存/缓存读取，无网络 IO
/// </summary>
public async Task<bool> IsModelSupportedAsync(AccountToken account, string requestedModel)
{
    var whitelist = GetModelMappingFromExtra(account.ExtraProperties);

    // 1. 有白名单配置 → 仅检查白名单（精确+通配）
    if (whitelist != null && whitelist.Count > 0)
    {
        if (whitelist.ContainsKey(requestedModel)) return true;
        return whitelist.Keys
            .Where(k => k.EndsWith('*'))
            .Any(k => requestedModel.StartsWith(k[..^1]));
    }

    // 2. 无白名单 → 尝试读上游模型缓存（仅读，不触发拉取）
    var cachedIds = await upstreamModelCacheDomainService.GetCachedModelIdsAsync(account.Id);
    if (cachedIds != null)
        return cachedIds.Contains(requestedModel);

    // 3. 缓存未命中 → 降级到平台静态模型（无 IO）
    var platformModels = modelProvider.GetAvailableModels(account.Platform);
    return platformModels.Count == 0 // 平台无限制则全通
        || platformModels.Any(m => m.Value.Equals(requestedModel, StringComparison.OrdinalIgnoreCase));
}
```

**在选号循环中注入（过滤5）**：

```csharp
// ProviderGroupDomainService.cs — foreach 过滤块末尾追加

// 过滤5：模型支持检查（仅聊天路径）
if (!string.IsNullOrEmpty(requestedModel)
    && !await accountTokenDomainService.IsModelSupportedAsync(relation.AccountToken, requestedModel))
{
    logger.LogDebug("账号 {AccountId} 不支持模型 {Model}，跳过",
        relation.AccountTokenId, requestedModel);
    continue;
}

availableRelations.Add(relation);
```

**`requestedModel` 来源**：`SelectProxyAccountInputDto` 新增字段，从 `SmartReverseProxyMiddleware` 透传：

```csharp
// SelectProxyAccountInputDto.cs
public string? RequestedModel { get; init; }
```

> `requestedModel` 为 null 时（非聊天路径）跳过过滤5，行为与现有逻辑一致。

---

### 4. UI 模型展示（AccountTokenAppService）

**入口1（模型测试dialog）的完整逻辑**：

```csharp
// AccountTokenAppService.cs
public async Task<IReadOnlyList<ModelOptionOutputDto>> GetAvailableModelsAsync(
    ProviderPlatform platform,
    Guid? accountId = null,
    CancellationToken cancellationToken = default)
{
    var baselineModels = modelProvider.GetAvailableModels(platform);

    // 无 accountId → 返回平台静态模型
    if (!accountId.HasValue)
        return objectMapper.Map<IReadOnlyList<ModelOption>, IReadOnlyList<ModelOptionOutputDto>>(baselineModels);

    var account = await accountTokenRepository.GetByIdAsync(accountId.Value, cancellationToken)
        ?? throw new NotFoundException($"账户不存在: {accountId}");

    var whitelist = AccountTokenDomainService.GetModelMappingFromExtra(account.ExtraProperties);

    // 1. 有白名单配置 → 直接返回白名单模型（仅白名单模式：key == value）
    if (whitelist != null && whitelist.Count > 0 && IsWhitelistMode(whitelist))
    {
        var whitelistModels = whitelist.Keys
            .Select(k => new ModelOption { Value = k, Label = k })
            .ToList();
        return objectMapper.Map<IReadOnlyList<ModelOption>, IReadOnlyList<ModelOptionOutputDto>>(whitelistModels);
    }

    // 2. 无白名单或映射模式 → 拉取上游模型并过滤（含缓存）
    await accountTokenDomainService.RefreshTokenIfNeededAsync(account, cancellationToken);

    var upstreamIds = await upstreamModelCacheDomainService.FetchAndCacheAsync(account, cancellationToken);

    IReadOnlyList<ModelOption> finalModels;
    if (upstreamIds != null && upstreamIds.Count > 0)
    {
        var upstreamSet = upstreamIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        finalModels = baselineModels.Where(m => upstreamSet.Contains(m.Value)).ToList();
    }
    else
    {
        finalModels = baselineModels; // 降级到静态
    }

    return objectMapper.Map<IReadOnlyList<ModelOption>, IReadOnlyList<ModelOptionOutputDto>>(finalModels);
}

// 判断是否为白名单模式（key == value 且无通配符）
private static bool IsWhitelistMode(Dictionary<string, string> mapping)
    => mapping.All(kv => kv.Key == kv.Value && !kv.Key.Contains('*'));
```

---

###

---

### 4. 请求处理（Processor 改造）

**改造原则**：Processor 通过构造函数注入 `ChatModelConnectionOptions`，从 `options.ExtraProperties` 读取账户级映射，未命中再走 `IModelProvider` 平台级映射。

**Processor 改造示例（以 AntigravityModelIdMappingProcessor 为例）**：

```csharp
public class AntigravityModelIdMappingProcessor(
    IModelProvider modelProvider,
    ChatModelConnectionOptions options) : IRequestProcessor
{
    public Task ProcessAsync(DownRequestContext down, UpRequestContext up, CancellationToken ct)
    {
        var modelId = string.IsNullOrEmpty(down.ModelId) ? "gemini-2.0-flash-exp" : down.ModelId;

        // 1. 账户级映射（从 Options.ExtraProperties 读取）
        var accountMapping = ModelMappingHelper.GetModelMapping(options.ExtraProperties);
        if (accountMapping != null)
        {
            var mapped = ModelMappingHelper.ResolveMapping(modelId, accountMapping);
            if (mapped != null)
            {
                up.MappedModelId = mapped;
                return Task.CompletedTask;
            }
        }

        // 2. 降级：平台级映射
        up.MappedModelId = modelProvider.GetAntigravityMappedModel(modelId);
        return Task.CompletedTask;
    }
}
```

**`ModelMappingHelper` 共享类**（`AiRelay.Infrastructure/.../Processors/ModelMappingHelper.cs`）：

```csharp
internal static class ModelMappingHelper
{
    public static Dictionary<string, string>? GetModelMapping(Dictionary<string, string> extraProperties)
    {
        if (!extraProperties.TryGetValue("model_mapping", out var json))
            return null;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
    }

    public static string? ResolveMapping(string model, Dictionary<string, string> mapping)
    {
        // 1. 精确匹配
        if (mapping.TryGetValue(model, out var exact))
            return exact;

        // 2. 通配符匹配（最长前缀优先）
        var match = mapping
            .Where(kv => kv.Key.EndsWith('*') && model.StartsWith(kv.Key[..^1]))
            .OrderByDescending(kv => kv.Key.Length)
            .FirstOrDefault();

        if (match.Key == null) return null;

        // 3. value 也以 * 结尾 → 前缀替换
        if (match.Value.EndsWith('*'))
        {
            var suffix = model[match.Key.Length - 1..];
            return match.Value[..^1] + suffix;
        }

        return match.Value;
    }
}
```

> `ClaudeModelIdMappingProcessor`、`OpenAiModelIdMappingProcessor` 同理改造。
> `GeminiOAuthModelIdMappingProcessor`、`GeminiApiKeyModelIdMappingProcessor` 新增，逻辑相同。

---

## DTO 变更

```csharp
// AccountTokenOutputDto.cs
public Dictionary<string, string>? ModelMapping { get; set; }

// CreateAccountTokenInputDto.cs / UpdateAccountTokenInputDto.cs
public Dictionary<string, string>? ModelMapping { get; set; }

// AccountTokenProfile.cs
CreateMap<AccountToken, AccountTokenOutputDto>()
    .ForMember(d => d.ModelMapping,
        o => o.MapFrom(s => AccountTokenDomainService.GetModelMappingFromExtra(s.ExtraProperties)));
```

---

## 性能说明

| 场景 | 耗时 | 说明 |
|------|------|------|
| 选号过滤（无映射，平台模型 List 匹配） | < 1μs | 内存列表遍历，无 IO |
| 选号过滤（有映射，Dict 精确匹配） | < 1μs | Dictionary O(1) |
| 选号过滤（通配符匹配） | < 2μs | 线性扫描 key 集合 |
| Processor 映射（含 Options.ExtraProperties） | < 1μs | 无额外序列化 |
| UI 模型列表（缓存命中） | < 5ms | Redis 读取 + 反序列化 |
| UI 模型列表（缓存未命中） | < 10s | 上游 API 拉取 + 写缓存 |

**缓存策略**：
- TTL：30-60分钟随机（避免缓存雪崩）
- Key：`account:models:{accountId}`
- 失效场景：账户 Token 刷新后自动失效（可选：在 `RefreshTokenIfNeededAsync` 后清除缓存）

---

## 前端设计

### 数据结构

```typescript
// account-token.dto.ts 新增字段
export interface AccountTokenOutputDto {
  // ...existing fields...
  modelMapping?: Record<string, string>;
}

export interface CreateAccountTokenInputDto {
  // ...existing fields...
  modelMapping?: Record<string, string>;
}

export interface UpdateAccountTokenInputDto {
  // ...existing fields...
  modelMapping?: Record<string, string>;
}
```

```typescript
// model-mapping.constants.ts（新文件）
import { ProviderPlatform } from '../models/provider-platform.enum';

// 各平台可选模型（用于白名单选择器下拉）
export const PLATFORM_MODEL_OPTIONS: Record<ProviderPlatform, string[]> = {
  [ProviderPlatform.GEMINI_OAUTH]:   ['gemini-2.5-pro', 'gemini-2.5-flash', 'gemini-2.0-flash'],
  [ProviderPlatform.GEMINI_APIKEY]:  ['gemini-2.5-pro', 'gemini-2.5-flash', 'gemini-2.0-flash'],
  [ProviderPlatform.CLAUDE_OAUTH]:   ['claude-opus-4-6', 'claude-sonnet-4-6', 'claude-haiku-4-5-20251001'],
  [ProviderPlatform.CLAUDE_APIKEY]:  ['claude-opus-4-6', 'claude-sonnet-4-6', 'claude-haiku-4-5-20251001'],
  [ProviderPlatform.OPENAI_OAUTH]:   ['gpt-5', 'gpt-5.1', 'gpt-5.1-codex', 'gpt-5.2'],
  [ProviderPlatform.OPENAI_APIKEY]:  ['gpt-5', 'gpt-5.1', 'gpt-5.1-codex', 'gpt-5.2'],
  [ProviderPlatform.ANTIGRAVITY]:    ['claude-sonnet-4-6', 'claude-opus-4-6', 'gemini-2.5-flash', 'gemini-2.5-pro'],
};

// 预设映射规则（快速填充）
export const PRESET_MAPPINGS: Record<ProviderPlatform, { label: string; from: string; to: string }[]> = {
  [ProviderPlatform.CLAUDE_APIKEY]: [
    { label: 'Opus→Sonnet', from: 'claude-opus-4-6', to: 'claude-sonnet-4-6' },
  ],
  [ProviderPlatform.ANTIGRAVITY]: [
    { label: 'Claude*→Sonnet', from: 'claude-*', to: 'claude-sonnet-4-5' },
    { label: 'Opus前缀替换', from: 'claude-opus-*', to: 'claude-sonnet-*' },
  ],
  // 其他平台留空
  [ProviderPlatform.GEMINI_OAUTH]:  [],
  [ProviderPlatform.GEMINI_APIKEY]: [],
  [ProviderPlatform.CLAUDE_OAUTH]:  [],
  [ProviderPlatform.OPENAI_OAUTH]:  [],
  [ProviderPlatform.OPENAI_APIKEY]: [],
};
```

### 编辑对话框逻辑

**核心认知**：白名单和映射规则是**两个独立的配置项**，可以同时配置：
- **白名单**（`model_whitelist`）：限制哪些模型可以被接受（key == value）
- **映射规则**（`model_mapping`）：对接受的模型进行名称转换（key → value）

**存储格式调整**：
```json
// ExtraProperties 中分为两个独立字段
{
  "model_whitelist": ["claude-sonnet-4-6", "claude-haiku-4-5-20251001"],  // 可选，字符串数组
  "model_mapping": {                                                       // 可选，映射规则
    "claude-opus-*": "claude-sonnet-*",
    "gemini-2.5-pro": "gemini-2.5-flash"
  }
}
```

**PrimeNG 组件选型**：

| 配置项 | 组件 | 说明 |
|--------|------|------|
| 白名单 | `p-autocomplete [multiple]="true" [dropdown]="true"` | 输入过滤预设 + 自定义输入 |
| 映射规则 | `p-inputgroup` + `input pInputText` | from / to 输入框组合 |
| 添加/删除 | `p-button` | icon="pi pi-plus" / "pi pi-trash" |

**模板（HTML）**：

```html
<!-- 白名单配置（独立区块） -->
<div class="field">
  <label>模型白名单（可选）</label>
  <p-autocomplete
    [(ngModel)]="modelWhitelist"
    [suggestions]="filteredModels"
    (completeMethod)="filterModels($event)"
    [multiple]="true"
    [dropdown]="true"
    [forceSelection]="false"
    placeholder="输入模型名或从下拉选择，留空则不限制"
    styleClass="w-full">
  </p-autocomplete>
  <small class="text-muted-color">
    配置后仅接受列表中的模型，支持自定义输入。留空则接受所有平台支持的模型。
  </small>
</div>

<!-- 映射规则配置（独立区块） -->
<div class="field">
  <label>模型映射规则（可选）</label>
  @if (modelMappings.length === 0) {
    <p class="text-muted-color mb-2">未配置映射规则，模型名称不转换</p>
  }
  @for (rule of modelMappings; track $index) {
    <p-inputgroup class="mb-2">
      <input pInputText
        [(ngModel)]="rule.from"
        placeholder="请求模型（如 claude-opus-4-6 或 claude-*）"
        class="flex-1" />
      <p-inputgroup-addon>
        <i class="pi pi-arrow-right"></i>
      </p-inputgroup-addon>
      <input pInputText
        [(ngModel)]="rule.to"
        placeholder="目标模型（如 claude-sonnet-4-6 或 claude-sonnet-*）"
        class="flex-1" />
      <p-inputgroup-addon>
        <p-button icon="pi pi-trash" severity="danger" variant="text"
          (click)="removeRule($index)" />
      </p-inputgroup-addon>
    </p-inputgroup>
    @if (getMappingHint(rule.from, rule.to)) {
      <small class="text-muted-color ml-2">
        {{ getMappingHint(rule.from, rule.to) }}
      </small>
    }
  }
  <p-button icon="pi pi-plus" label="添加映射规则" severity="secondary"
    [outlined]="true" (click)="addRule()" class="mt-2" />
  <small class="text-muted-color block mt-2">
    将请求的模型名转换为实际调用的模型名，支持通配符（如 claude-* → claude-sonnet-4-5）
  </small>
</div>
```

**组件逻辑（TypeScript）**：

```typescript
// account-edit-dialog 核心逻辑
// 白名单和映射规则是独立的两个配置项
modelWhitelist: string[] = [];             // p-autocomplete 绑定
filteredModels: string[] = [];             // 过滤后的建议列表
modelMappings: { from: string; to: string }[] = [];

// 平台预设模型（从 PLATFORM_MODEL_OPTIONS 常量读取）
get platformModels(): string[] {
  return PLATFORM_MODEL_OPTIONS[this.account.platform] ?? [];
}

// p-autocomplete 过滤方法
filterModels(event: AutoCompleteCompleteEvent): void {
  const query = event.query.toLowerCase();
  this.filteredModels = this.platformModels.filter(m =>
    m.toLowerCase().includes(query)
  );
}

addRule(): void {
  this.modelMappings = [...this.modelMappings, { from: '', to: '' }];
}

removeRule(index: number): void {
  this.modelMappings = this.modelMappings.filter((_, i) => i !== index);
}

// 初始化：从 extraProperties 读取两个独立字段
initFromExtraProperties(extra?: Record<string, string>): void {
  if (!extra) return;

  // 白名单
  if (extra['model_whitelist']) {
    try { this.modelWhitelist = JSON.parse(extra['model_whitelist']); }
    catch { this.modelWhitelist = []; }
  }

  // 映射规则
  if (extra['model_mapping']) {
    try {
      const mapping: Record<string, string> = JSON.parse(extra['model_mapping']);
      this.modelMappings = Object.entries(mapping).map(([from, to]) => ({ from, to }));
    } catch { this.modelMappings = []; }
  }
}

// 保存时写回 extraProperties
buildExtraProperties(base: Record<string, string>): Record<string, string> {
  const extra = { ...base };

  // 白名单
  if (this.modelWhitelist.length > 0)
    extra['model_whitelist'] = JSON.stringify(this.modelWhitelist);
  else
    delete extra['model_whitelist'];

  // 映射规则
  const validMappings = this.modelMappings
    .filter(m => m.from.trim() && m.to.trim() && this.isValidPattern(m.from));
  if (validMappings.length > 0)
    extra['model_mapping'] = JSON.stringify(
      Object.fromEntries(validMappings.map(m => [m.from.trim(), m.to.trim()]))
    );
  else
    delete extra['model_mapping'];

  return extra;
}

// 通配符校验：* 只能在末尾且只有一个
isValidPattern(pattern: string): boolean {
  const idx = pattern.indexOf('*');
  return idx === -1 || idx === pattern.length - 1;
}

// 获取前缀替换示例提示
getMappingHint(from: string, to: string): string {
  if (!from.endsWith('*') || !to.endsWith('*')) return '';
  const fromPrefix = from.slice(0, -1);
  const toPrefix = to.slice(0, -1);
  return `例：${fromPrefix}flash → ${toPrefix}flash`;
}
```

---

## Mock 数据补充

### `_mock/data/account-token.ts`

在现有 `ACCOUNT_TOKENS` 中补充 `modelMapping` 字段示例：

```typescript
// id:4 claude-relay-01 — 白名单模式
{
  id: '4',
  name: 'claude-relay-01',
  platform: ProviderPlatform.CLAUDE_APIKEY,
  // ...existing fields...
  modelMapping: {
    'claude-sonnet-4-6': 'claude-sonnet-4-6',
    'claude-haiku-4-5-20251001': 'claude-haiku-4-5-20251001'
  }
},

// id:8 antigravity-prod-01 — 映射模式（含前缀替换）
{
  id: '8',
  name: 'antigravity-prod-01',
  platform: ProviderPlatform.ANTIGRAVITY,
  // ...existing fields...
  modelMapping: {
    'claude-opus-*': 'claude-sonnet-*',
    'claude-*': 'claude-sonnet-4-5',
    'gemini-2.5-pro': 'gemini-2.5-flash'
  }
},

// id:10 antigravity-thinking — 无配置（降级到平台默认）
{
  id: '10',
  // ...existing fields...
  // modelMapping 未设置，使用平台默认模型列表
}
```

### `_mock/api/` 处理

无需新增接口，`modelMapping` 作为现有 CRUD 接口的字段扩展：
- `GET /api/account-tokens` → 列表返回含 `modelMapping`
- `GET /api/account-tokens/:id` → 详情返回含 `modelMapping`
- `POST /api/account-tokens` → 接收 `modelMapping` 并回显
- `PUT /api/account-tokens/:id` → 接收 `modelMapping` 并回显

---

## 实施顺序

1. **Domain**：
   - `UpstreamModelCacheDomainService` 新增（上游模型 ID 缓存服务）
   - `AccountTokenDomainService` 新增：
     - `GetModelMappingFromExtra`（static，供 Processor 调用）
     - `IsModelSupportedAsync`（选号用，注入 `IModelProvider` + `UpstreamModelCacheDomainService`）

2. **Application**：
   - DTO 扩展 `ModelMapping`
   - `AccountTokenProfile` 映射（`GetModelMappingFromExtra` 读取）
   - `AccountTokenAppService` Create/Update 处理 `model_mapping` 序列化
   - `AccountTokenAppService.GetAvailableModelsAsync` 改造（白名单优先，否则调用 `UpstreamModelCacheDomainService`）
   - `SelectProxyAccountInputDto` 新增 `RequestedModel`

3. **Infrastructure**：
   - `ModelMappingHelper.cs` 共享类（`GetModelMapping` + `ResolveMapping`）
   - 各平台 `ModelIdMappingProcessor` 改造（注入 `ChatModelConnectionOptions`）
   - Gemini 新增两个 Processor

4. **Domain（选号）**：`ProviderGroupDomainService` 注入 `AccountTokenDomainService`，添加过滤5

5. **Frontend**：`model-mapping.constants.ts` → 白名单编辑器（`p-autocomplete`）+ 映射规则编辑器（`p-inputgroup`）→ 编辑对话框改造 → Mock 数据补充
