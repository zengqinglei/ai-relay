# Antigravity 平台集成完整实施方案

## 📋 方案概览

本方案在前后端同步实现 Antigravity 平台支持，包括：
- **前端**：UI 组件、图标、标签、模型测试、Mock 数据
- **后端**：枚举、模型映射、ChatModelClient、ReverseProxy、模型测试 API

---

## 📂 文件变更树形目录

### 前端项目（frontend-gemini）

```
frontend-gemini/
├── src/
│   └── app/
│       ├── shared/
│       │   ├── models/
│       │   │   └── provider-platform.enum.ts                    [修改] 新增 ANTIGRAVITY 枚举值
│       │   ├── constants/
│       │   │   └── provider-platform.constants.ts               [修改] 新增 Antigravity 标签
│       │   ├── components/
│       │   │   └── platform-icon/
│       │   │       └── platform-icon.ts                         [修改] 新增 Antigravity 图标和颜色
│       │   └── pipes/
│       │       └── platform-label-pipe.ts                       [无需修改] 自动支持新枚举
│       └── features/
│           └── platform/
│               └── components/
│                   ├── subscriptions/
│                   │   └── widgets/
│                   │       └── subscription-table/
│                   │           └── subscription-table.ts        [修改] 添加颜色标记 Emoji
│                   └── provider-account/
│                       └── widgets/
│                           └── model-test-dialog/
│                               └── model-test-dialog.ts         [修改] 新增 Antigravity 模型列表
└── _mock/
    └── data/
        ├── provider-account.ts                                  [修改] 新增 Antigravity Mock 数据
        ├── provider-group.ts                                    [修改] 更新平台分布统计
        └── subscriptions.ts                                     [修改] 新增 Antigravity 订阅数据
```

### 后端项目（backend/src）

```
backend/src/
├── AiRelay.Domain/
│   └── ProviderAccounts/
│       ├── ValueObjects/
│       │   └── ProviderPlatform.cs                              [修改] 新增 ANTIGRAVITY 枚举值
│       ├── Extensions/
│       │   └── ProviderPlatformExtensions.cs                    [修改] 新增平台判断扩展方法
│       ├── Services/
│       │   └── IModelMappingService.cs                          [新增] 模型映射服务接口
│       └── Entities/
│           └── AccountToken.cs                                  [可选修改] 新增 ModelMappings 字段
├── AiRelay.Domain.Shared/
│   └── ExternalServices/
│       └── ChatModel/
│           └── Dto/
│               └── ChatModelConnectionOptions.cs                [修改] 新增 ModelMappings 属性
├── AiRelay.Infrastructure/
│   ├── ProviderAccounts/
│   │   └── Services/
│   │       └── ModelMappingService.cs                           [新增] 模型映射服务实现
│   ├── Shared/
│   │   └── ExternalServices/
│   │       └── ChatModel/
│   │           ├── AntigravityChatModelClient.cs                [新增] Antigravity 客户端
│   │           └── ChatModelClientFactory.cs                    [修改] 注册 Antigravity 客户端
│   └── DependencyInjection.cs                                   [修改] 注册新服务
└── AiRelay.Api/
    ├── Transforms/
    │   ├── AntigravityRequestTransform.cs                       [新增] Antigravity 请求转换器
    │   └── RequestTransformProvider.cs                          [修改] 注册 Antigravity Transform
    └── appsettings.json                                         [修改] 新增 ReverseProxy 路由和集群
```

**统计**：
- 前端：修改 6 个文件，新增 Mock 数据
- 后端：修改 7 个文件，新增 3 个文件

---

# 一、前端实现方案（frontend-gemini）

## 1.1 枚举定义

### 文件：`src/app/shared/models/provider-platform.enum.ts`

```typescript
export enum ProviderPlatform {
  GEMINI_ACCOUNT = 'GEMINI_ACCOUNT',
  GEMINI_APIKEY = 'GEMINI_APIKEY',
  CLAUDE_ACCOUNT = 'CLAUDE_ACCOUNT',
  CLAUDE_APIKEY = 'CLAUDE_APIKEY',
  OPENAI_ACCOUNT = 'OPENAI_ACCOUNT',
  OPENAI_APIKEY = 'OPENAI_APIKEY',

  // 新增 Antigravity 平台
  ANTIGRAVITY = 'ANTIGRAVITY'
}
```

**说明**：
- Antigravity 使用单一枚举值，因为它本身就是通过 OAuth 认证的统一代理
- 不区分 ACCOUNT/APIKEY，简化用户理解

---

## 1.2 共享组件

### 1.2.1 平台标签配置

**文件：`src/app/shared/constants/provider-platform.constants.ts`**

```typescript
export const PROVIDER_PLATFORM_LABELS: Record<ProviderPlatform, string> = {
  [ProviderPlatform.GEMINI_ACCOUNT]: 'Gemini 账户',
  [ProviderPlatform.GEMINI_APIKEY]: 'Gemini API Key',
  [ProviderPlatform.CLAUDE_ACCOUNT]: 'Claude 账户',
  [ProviderPlatform.CLAUDE_APIKEY]: 'Claude API Key',
  [ProviderPlatform.OPENAI_ACCOUNT]: 'OpenAI 账户',
  [ProviderPlatform.OPENAI_APIKEY]: 'OpenAI API Key',

  // 新增 Antigravity
  [ProviderPlatform.ANTIGRAVITY]: 'Antigravity'
};
```

### 1.2.2 平台图标组件

**文件：`src/app/shared/components/platform-icon/platform-icon.ts`**

**修改点 1：添加颜色类**

```typescript
// 在 ngClass 中添加 Antigravity 颜色
[ngClass]="{
  'text-blue-600 dark:text-blue-400': platform().includes('GEMINI'),
  'text-orange-600 dark:text-orange-500': platform().includes('CLAUDE'),
  'text-surface-900 dark:text-surface-0': platform().includes('OPENAI'),
  'text-purple-600 dark:text-purple-400': platform().includes('ANTIGRAVITY')  // 新增：紫色
}"
```

**修改点 2：添加 SVG 图标**

在模板中添加 Antigravity 图标（火箭/上升箭头主题）：

```html
<!-- 在现有图标后添加 -->
@else if (platform().includes('ANTIGRAVITY')) {
  <!-- Antigravity 图标：火箭/上升箭头组合 -->
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor">
    <!-- 主体火箭 -->
    <path d="M12 2L4 10h3v9a1 1 0 001 1h8a1 1 0 001-1v-9h3l-8-8z"/>
    <!-- 火焰/推力 -->
    <path opacity="0.6" d="M9 20v2a1 1 0 001 1h4a1 1 0 001-1v-2H9z"/>
    <!-- 窗口装饰 -->
    <circle cx="12" cy="13" r="1.5" fill="white" opacity="0.8"/>
  </svg>
}
```

**替代方案（极简风格）**：

```html
@else if (platform().includes('ANTIGRAVITY')) {
  <!-- 极简上升箭头 -->
  <svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
    <path d="M12 2v20M5 9l7-7 7 7"/>
    <circle cx="12" cy="8" r="2" fill="currentColor"/>
  </svg>
}
```

---

## 1.3 订阅管理页面

### 文件：`src/app/features/platform/components/subscriptions/widgets/subscription-table/subscription-table.ts`

**添加颜色标记方法**：

```typescript
getBindingsTooltip(bindings: ApiKeyBindingOutputDto[]): string {
  if (!bindings || bindings.length === 0) return '';

  return bindings.map(b => {
    const emoji = this.getPlatformEmoji(b.platform);
    return `${emoji} ${b.providerGroupName} (${this.getPlatformLabel(b.platform)})`;
  }).join('\n');
}

private getPlatformEmoji(platform: string): string {
  if (platform.includes('GEMINI')) return '🔵';      // 蓝色圆
  if (platform.includes('CLAUDE')) return '🟠';      // 橙色圆
  if (platform.includes('OPENAI')) return '⚫';      // 黑色圆
  if (platform.includes('ANTIGRAVITY')) return '🟣'; // 紫色圆
  return '⚪';
}

private getPlatformLabel(platform: string): string {
  return PROVIDER_PLATFORM_LABELS[platform as ProviderPlatform] || platform;
}
```

**说明**：使用 Emoji 标记，无需修改 UI 布局，简单直观。

---

## 1.4 模型测试页面

### 文件：`src/app/features/platform/components/provider-account/widgets/model-test-dialog/model-test-dialog.ts`

**修改：添加 Antigravity 模型列表**

```typescript
modelOptions = computed(() => {
  const acc = this.account();
  if (!acc) return [];

  switch (true) {
    case acc.platform.includes('ANTIGRAVITY'):
      return [
        // Gemini 模型（通过 Antigravity 代理）
        { label: 'Gemini 2.5 Flash', value: 'gemini-2.5-flash', category: 'Gemini' },
        { label: 'Gemini 2.0 Flash (Experimental)', value: 'gemini-2.0-flash-exp', category: 'Gemini' },
        { label: 'Gemini 2.0 Flash Thinking', value: 'gemini-2.0-flash-thinking-exp', category: 'Gemini' },
        { label: 'Gemini 1.5 Pro', value: 'gemini-1.5-pro', category: 'Gemini' },
        { label: 'Gemini 1.5 Flash', value: 'gemini-1.5-flash', category: 'Gemini' },

        // Claude 模型（通过 Antigravity 代理）
        { label: 'Claude Opus 4.5', value: 'claude-opus-4-5-20251101', category: 'Claude' },
        { label: 'Claude Sonnet 4.5', value: 'claude-sonnet-4-5-20250514', category: 'Claude' },
        { label: 'Claude 3.7 Sonnet', value: 'claude-3-7-sonnet-20250219', category: 'Claude' },
        { label: 'Claude 3.5 Sonnet (Oct 2024)', value: 'claude-3-5-sonnet-20241022', category: 'Claude' }
      ];

    case acc.platform.includes('GEMINI'):
      return [
        { label: 'Gemini 2.0 Flash (Experimental)', value: 'gemini-2.0-flash-exp' },
        { label: 'Gemini 2.5 Flash Thinking Preview', value: 'gemini-2.5-flash-thinking-preview-12-12' },
        { label: 'Gemini Experimental 1206', value: 'gemini-exp-1206' },
        { label: 'Gemini 1.5 Pro', value: 'gemini-1.5-pro-latest' },
        { label: 'Gemini 1.5 Flash', value: 'gemini-1.5-flash-latest' },
        { label: 'Gemini 1.5 Flash 8B', value: 'gemini-1.5-flash-8b-latest' },
        { label: 'Gemini 2.0 Flash Thinking', value: 'gemini-2.0-flash-thinking-exp-1219' },
        { label: 'Gemini 2.0 Flash', value: 'gemini-2.0-flash' }
      ];

    case acc.platform.includes('CLAUDE'):
      return [
        { label: 'Claude Opus 4.5', value: 'claude-opus-4-5-20251101' },
        { label: 'Claude Sonnet 4.5', value: 'claude-sonnet-4-5-20250514' },
        { label: 'Claude 3.7 Sonnet', value: 'claude-3-7-sonnet-20250219' },
        { label: 'Claude 3.5 Sonnet (Oct 2024)', value: 'claude-3-5-sonnet-20241022' },
        { label: 'Claude 3.5 Sonnet (Jun 2024)', value: 'claude-3-5-sonnet-20240620' },
        { label: 'Claude 3 Opus', value: 'claude-3-opus-20240229' }
      ];

    case acc.platform.includes('OPENAI'):
      return [
        { label: 'OpenAI o1', value: 'o1' },
        { label: 'OpenAI o1-mini', value: 'o1-mini' },
        { label: 'GPT-4o', value: 'gpt-4o' },
        { label: 'GPT-4 Turbo', value: 'gpt-4-turbo' },
        { label: 'GPT-3.5 Turbo', value: 'gpt-3.5-turbo' }
      ];

    default:
      return [{ label: 'Default Model', value: 'default' }];
  }
});
```

**优化：添加模型分组显示**（可选）

```html
<!-- 在模板中使用 p-select 的分组功能 -->
<p-select
  [options]="groupedModelOptions()"
  [(ngModel)]="selectedModel"
  optionLabel="label"
  optionValue="value"
  [group]="true"
  optionGroupLabel="category"
  optionGroupChildren="models"
  [fluid]="true">
</p-select>
```

```typescript
groupedModelOptions = computed(() => {
  const acc = this.account();
  if (!acc || !acc.platform.includes('ANTIGRAVITY')) {
    return this.modelOptions(); // 非 Antigravity 保持原样
  }

  const models = this.modelOptions();
  return [
    {
      category: 'Gemini 模型',
      models: models.filter(m => m.category === 'Gemini')
    },
    {
      category: 'Claude 模型',
      models: models.filter(m => m.category === 'Claude')
    }
  ];
});
```

---

## 1.5 Mock 数据

### 1.5.1 Provider Account Mock

**文件：`_mock/data/provider-account.ts`**

```typescript
// 添加 Antigravity 账号 Mock 数据
export const mockProviderAccounts: ProviderAccountOutputDto[] = [
  // ... 现有 Mock 数据 ...

  // Antigravity 账号
  {
    id: 'antigravity-001',
    name: 'Antigravity 主账号',
    platform: ProviderPlatform.ANTIGRAVITY,
    priority: 1,
    isEnabled: true,
    concurrency: 10,
    providerGroupId: 'group-001',
    providerGroupName: 'AI 服务商分组 1',
    accountTokens: [
      {
        id: 'token-antigravity-001',
        accessToken: 'ya29.a0AfB_byC...(Mock OAuth Token)',
        refreshToken: 'refresh_token_mock',
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        projectId: 'antigravity-project-123',
        baseUrl: '',
        isEnabled: true
      }
    ],
    createdAt: '2026-01-15T10:00:00Z',
    updatedAt: '2026-01-28T12:30:00Z'
  }
];
```

### 1.5.2 Provider Group Mock

**文件：`_mock/data/provider-group.ts`**

```typescript
export const mockProviderGroups: ProviderGroupOutputDto[] = [
  {
    id: 'group-001',
    name: 'AI 服务商分组 1',
    description: '包含 Gemini、Claude、OpenAI 和 Antigravity 账号',
    accountCount: 8, // 更新数量（原有 7 + 新增 1）
    platformDistribution: {
      GEMINI_ACCOUNT: 2,
      GEMINI_APIKEY: 1,
      CLAUDE_ACCOUNT: 1,
      CLAUDE_APIKEY: 1,
      OPENAI_ACCOUNT: 2,
      ANTIGRAVITY: 1  // 新增
    }
  }
];
```

### 1.5.3 Subscription Mock

**文件：`_mock/data/subscriptions.ts`**

```typescript
export const mockSubscriptions: SubscriptionOutputDto[] = [
  // ... 现有订阅 ...

  // Antigravity 订阅
  {
    id: 'sub-antigravity-001',
    userId: 'user-001',
    apiKey: 'ak-antigravity-xxxxxxxx',
    bindings: [
      {
        id: 'binding-antigravity-001',
        providerGroupId: 'group-001',
        providerGroupName: 'AI 服务商分组 1',
        platform: ProviderPlatform.ANTIGRAVITY
      }
    ],
    requestCount: 150,
    tokenUsage: 45000,
    createdAt: '2026-01-20T08:00:00Z'
  }
];
```

---

# 二、后端实现方案（backend/src）

## 2.1 枚举定义

### 文件：`AiRelay.Domain/ProviderAccounts/ValueObjects/ProviderPlatform.cs`

```csharp
public enum ProviderPlatform
{
    GEMINI_ACCOUNT,
    GEMINI_APIKEY,
    CLAUDE_ACCOUNT,
    CLAUDE_APIKEY,
    OPENAI_ACCOUNT,
    OPENAI_APIKEY,

    ANTIGRAVITY  // 新增
}
```

### 文件：`AiRelay.Domain/ProviderAccounts/Extensions/ProviderPlatformExtensions.cs`

```csharp
public static class ProviderPlatformExtensions
{
    public static bool IsApiKeyPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_APIKEY
               || platform == ProviderPlatform.CLAUDE_APIKEY
               || platform == ProviderPlatform.OPENAI_APIKEY;
        // Antigravity 不是 API Key 平台，使用 OAuth
    }

    /// <summary>
    /// 判断是否为 Antigravity 平台
    /// </summary>
    public static bool IsAntigravityPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.ANTIGRAVITY;
    }

    /// <summary>
    /// 判断是否为 Gemini 系列平台
    /// </summary>
    public static bool IsGeminiPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.GEMINI_ACCOUNT
               || platform == ProviderPlatform.GEMINI_APIKEY;
    }

    /// <summary>
    /// 判断是否为 Claude 系列平台
    /// </summary>
    public static bool IsClaudePlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.CLAUDE_ACCOUNT
               || platform == ProviderPlatform.CLAUDE_APIKEY;
    }

    /// <summary>
    /// 判断是否为 OpenAI 系列平台
    /// </summary>
    public static bool IsOpenAIPlatform(this ProviderPlatform platform)
    {
        return platform == ProviderPlatform.OPENAI_ACCOUNT
               || platform == ProviderPlatform.OPENAI_APIKEY;
    }
}
```

---

## 2.2 模型映射机制

### 新增文件：`AiRelay.Domain/ProviderAccounts/Services/IModelMappingService.cs`

```csharp
namespace AiRelay.Domain.ProviderAccounts.Services;

/// <summary>
/// 模型映射服务接口（用于 Antigravity 模型名称映射）
/// </summary>
public interface IModelMappingService
{
    /// <summary>
    /// 获取映射后的模型名称
    /// </summary>
    /// <param name="platform">平台类型</param>
    /// <param name="requestedModel">客户端请求的模型名称</param>
    /// <param name="accountModelMappings">账号级自定义映射（可选）</param>
    /// <returns>实际调用的模型名称</returns>
    string GetMappedModel(
        ProviderPlatform platform,
        string requestedModel,
        Dictionary<string, string>? accountModelMappings = null);
}
```

### 新增文件：`AiRelay.Infrastructure/ProviderAccounts/Services/ModelMappingService.cs`

```csharp
namespace AiRelay.Infrastructure.ProviderAccounts.Services;

public class ModelMappingService : IModelMappingService
{
    private readonly ILogger<ModelMappingService> _logger;

    // Antigravity 全局模型映射配置
    private static readonly Dictionary<string, string> AntigravityGlobalMappings = new()
    {
        // Claude 模型前缀映射（处理版本号变化）
        ["claude-opus-4-5"] = "claude-opus-4-5-20251101",
        ["claude-sonnet-4-5"] = "claude-sonnet-4-5-20250514",
        ["claude-3-7-sonnet"] = "claude-3-7-sonnet-20250219",
        ["claude-3-5-sonnet"] = "claude-3-5-sonnet-20241022",

        // Gemini 模型别名
        ["gemini-2-flash"] = "gemini-2.0-flash-exp",
        ["gemini-flash"] = "gemini-2.0-flash-exp"
    };

    // Antigravity 直接支持的模型列表（无需映射）
    private static readonly HashSet<string> AntigravitySupportedModels = new()
    {
        // Gemini 模型
        "gemini-2.5-flash",
        "gemini-2.0-flash-exp",
        "gemini-2.0-flash-thinking-exp",
        "gemini-1.5-pro",
        "gemini-1.5-flash",

        // Claude 模型（完整版本号）
        "claude-opus-4-5-20251101",
        "claude-sonnet-4-5-20250514",
        "claude-3-7-sonnet-20250219",
        "claude-3-5-sonnet-20241022",
        "claude-3-5-sonnet-20240620",
        "claude-3-opus-20240229"
    };

    public ModelMappingService(ILogger<ModelMappingService> logger)
    {
        _logger = logger;
    }

    public string GetMappedModel(
        ProviderPlatform platform,
        string requestedModel,
        Dictionary<string, string>? accountModelMappings = null)
    {
        // 仅 Antigravity 平台需要映射
        if (platform != ProviderPlatform.ANTIGRAVITY)
        {
            return requestedModel;
        }

        // 1. 优先使用账号级自定义映射
        if (accountModelMappings?.TryGetValue(requestedModel, out var accountMapped) == true)
        {
            _logger.LogDebug("使用账号级映射：{RequestedModel} -> {MappedModel}", requestedModel, accountMapped);
            return accountMapped;
        }

        // 2. 检查是否直接支持
        if (AntigravitySupportedModels.Contains(requestedModel))
        {
            return requestedModel;
        }

        // 3. 使用全局映射
        if (AntigravityGlobalMappings.TryGetValue(requestedModel, out var globalMapped))
        {
            _logger.LogDebug("使用全局映射：{RequestedModel} -> {MappedModel}", requestedModel, globalMapped);
            return globalMapped;
        }

        // 4. 前缀匹配（处理模型族）
        foreach (var (prefix, target) in AntigravityGlobalMappings)
        {
            if (requestedModel.StartsWith(prefix))
            {
                _logger.LogDebug("使用前缀映射：{RequestedModel} -> {MappedModel}", requestedModel, target);
                return target;
            }
        }

        // 5. Gemini 模型透传（以 gemini- 开头的模型直接使用）
        if (requestedModel.StartsWith("gemini-"))
        {
            _logger.LogDebug("Gemini 模型透传：{RequestedModel}", requestedModel);
            return requestedModel;
        }

        // 6. 默认值（降级到 Gemini Flash）
        _logger.LogWarning("未找到模型映射，使用默认模型：{RequestedModel} -> gemini-2.0-flash-exp", requestedModel);
        return "gemini-2.0-flash-exp";
    }
}
```

---

## 2.3 ChatModelClient 实现

### 更新 DTO：`AiRelay.Domain.Shared/ExternalServices/ChatModel/Dto/ChatModelConnectionOptions.cs`

```csharp
public record ChatModelConnectionOptions
{
    public required string Credential { get; init; }
    public string? BaseUrl { get; init; }
    public string? ProjectId { get; init; }

    // 新增：模型映射配置（账号级）
    public Dictionary<string, string>? ModelMappings { get; init; }
}
```

### 新增文件：`AiRelay.Infrastructure/Shared/ExternalServices/ChatModel/AntigravityChatModelClient.cs`

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using AiRelay.Domain.ProviderAccounts.Services;
using AiRelay.Domain.Shared.ExternalServices.ChatModel;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using Microsoft.Extensions.Logging;

namespace AiRelay.Infrastructure.Shared.ExternalServices.ChatModel;

public class AntigravityChatModelClient(
    IHttpClientFactory httpClientFactory,
    IModelMappingService modelMappingService,
    ILogger<AntigravityChatModelClient> logger) : IChatModelClient
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient();
    private ChatModelConnectionOptions? _options;

    private const string PrimaryEndpoint = "https://generativelanguage.googleapis.com";
    private const string FallbackEndpoint = "https://generativelanguage-daily.googleapis.com";
    private const string ApiVersion = "v1internal";
    private const string AntigravityIdentity =
        "You are Antigravity, a powerful agentic AI coding assistant designed by the Google Deepmind team working on Advanced Agentic Coding.";

    private string _currentEndpoint = PrimaryEndpoint;

    public void Configure(ChatModelConnectionOptions options)
    {
        _options = options;
    }

    public Task<ConnectionValidationResult> ValidateConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new ConnectionValidationResult(true));
    }

    public async IAsyncEnumerable<ChatStreamEvent> StreamChatAsync(
        string modelId,
        string message,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (_options == null)
        {
            yield return new ChatStreamEvent(Error: "Client not configured");
            yield break;
        }

        // 模型映射（使用 ModelMappingService）
        var mappedModel = modelMappingService.GetMappedModel(
            Domain.ProviderAccounts.ValueObjects.ProviderPlatform.ANTIGRAVITY,
            modelId,
            _options.ModelMappings);

        var maxRetries = 2;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            var url = $"{_currentEndpoint}/{ApiVersion}:streamGenerateContent?alt=sse";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.Credential);
            request.Headers.Add("User-Agent", "antigravity/1.0");

            // 构建 v1internal 请求（统一格式，支持所有模型）
            var requestBody = new
            {
                project = _options.ProjectId ?? "",
                requestId = $"agent-{Guid.NewGuid()}",
                userAgent = "antigravity",
                requestType = "agent",
                model = mappedModel,
                request = new
                {
                    contents = new[]
                    {
                        new
                        {
                            role = "user",
                            parts = new[] { new { text = message } }
                        }
                    },
                    systemInstruction = new
                    {
                        parts = new[]
                        {
                            new { text = AntigravityIdentity }
                        }
                    }
                }
            };

            request.Content = JsonContent.Create(requestBody);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // 自动故障转移
            if (!response.IsSuccessStatusCode && attempt == 0 && _currentEndpoint == PrimaryEndpoint)
            {
                logger.LogWarning("Antigravity 主端点失败 ({StatusCode})，切换到备用端点", response.StatusCode);
                _currentEndpoint = FallbackEndpoint;
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Antigravity API 调用失败 - StatusCode: {StatusCode}, Error: {Error}", response.StatusCode, error);
                yield return new ChatStreamEvent(Error: $"Antigravity API 错误 ({response.StatusCode}): {error}");
                yield break;
            }

            // 统一的 SSE 流解析（所有模型都返回相同格式）
            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
            {
                if (line.StartsWith("data: "))
                {
                    var json = line.Substring(6);
                    if (json.Trim() == "[DONE]") break;

                    string? text = null;
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        if (doc.RootElement.TryGetProperty("response", out var responseObj) &&
                            responseObj.TryGetProperty("candidates", out var candidates) &&
                            candidates.GetArrayLength() > 0)
                        {
                            var candidate = candidates[0];
                            if (candidate.TryGetProperty("content", out var content) &&
                                content.TryGetProperty("parts", out var parts) &&
                                parts.GetArrayLength() > 0)
                            {
                                text = parts[0].GetProperty("text").GetString();
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        logger.LogWarning(ex, "解析 Antigravity SSE 事件失败，已跳过 - JSON: {Json}", json);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "处理 Antigravity SSE 事件时发生未知错误 - JSON: {Json}", json);
                    }

                    if (!string.IsNullOrEmpty(text))
                    {
                        yield return new ChatStreamEvent(Content: text);
                    }
                }
            }

            yield return new ChatStreamEvent(IsComplete: true);
            yield break;
        }
    }
}
```

---

## 2.4 工厂和 DI 注册

### 更新：`AiRelay.Infrastructure/Shared/ExternalServices/ChatModel/ChatModelClientFactory.cs`

```csharp
public class ChatModelClientFactory(IServiceProvider serviceProvider) : IChatModelClientFactory
{
    public IChatModelClient CreateClient(AccountToken account)
    {
        var options = new ChatModelConnectionOptions
        {
            Credential = account.AccessToken,
            BaseUrl = account.BaseUrl,
            ProjectId = account.ProjectId,
            ModelMappings = account.ModelMappings  // 新增
        };

        return CreateClient(account.Platform, options);
    }

    public IChatModelClient CreateClient(ProviderPlatform platform, ChatModelConnectionOptions options)
    {
        IChatModelClient client = platform switch
        {
            ProviderPlatform.GEMINI_ACCOUNT => serviceProvider.GetRequiredKeyedService<IChatModelClient>("GeminiAccount"),
            ProviderPlatform.GEMINI_APIKEY => serviceProvider.GetRequiredKeyedService<IChatModelClient>("GeminiApi"),
            ProviderPlatform.CLAUDE_ACCOUNT => serviceProvider.GetRequiredKeyedService<IChatModelClient>("ClaudeAccount"),
            ProviderPlatform.CLAUDE_APIKEY => serviceProvider.GetRequiredKeyedService<IChatModelClient>("ClaudeApi"),
            ProviderPlatform.OPENAI_ACCOUNT => serviceProvider.GetRequiredKeyedService<IChatModelClient>("OpenAiAccount"),
            ProviderPlatform.OPENAI_APIKEY => serviceProvider.GetRequiredKeyedService<IChatModelClient>("OpenAiApi"),

            ProviderPlatform.ANTIGRAVITY => serviceProvider.GetRequiredKeyedService<IChatModelClient>("Antigravity"),

            _ => throw new NotSupportedException($"Platform {platform} is not supported")
        };

        client.Configure(options);
        return client;
    }
}
```

### 更新：`AiRelay.Infrastructure/DependencyInjection.cs`

```csharp
public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
{
    // ... 现有服务 ...

    // 注册模型映射服务
    services.AddSingleton<IModelMappingService, ModelMappingService>();

    // 注册 ChatModel 客户端
    services.AddKeyedTransient<IChatModelClient, GeminiAccountChatModelClient>("GeminiAccount");
    services.AddKeyedTransient<IChatModelClient, GeminiApiChatModelClient>("GeminiApi");
    services.AddKeyedTransient<IChatModelClient, ClaudeChatModelClient>("ClaudeAccount");
    services.AddKeyedTransient<IChatModelClient, ClaudeChatModelClient>("ClaudeApi");
    services.AddKeyedTransient<IChatModelClient, OpenAiChatModelClient>("OpenAiAccount");
    services.AddKeyedTransient<IChatModelClient, OpenAiChatModelClient>("OpenAiApi");

    // 新增 Antigravity 客户端
    services.AddKeyedTransient<IChatModelClient, AntigravityChatModelClient>("Antigravity");

    services.AddTransient<IChatModelClientFactory, ChatModelClientFactory>();

    return services;
}
```

---

## 2.5 ReverseProxy 配置

### 更新：`AiRelay.Api/appsettings.json`

```json
{
  "ReverseProxy": {
    "Routes": {
      "GeminiAccountRoute": {
        "ClusterId": "GeminiAccountCluster",
        "Match": { "Path": "/gemini/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "GEMINI_ACCOUNT" },
        "Transforms": [{ "PathRemovePrefix": "/gemini" }]
      },
      "GeminiApiKeyRoute": {
        "ClusterId": "GeminiApiKeyCluster",
        "Match": { "Path": "/gemini-api/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "GEMINI_APIKEY" },
        "Transforms": [{ "PathRemovePrefix": "/gemini-api" }]
      },
      "ClaudeAccountRoute": {
        "ClusterId": "ClaudeAccountCluster",
        "Match": { "Path": "/claude/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "CLAUDE_ACCOUNT" },
        "Transforms": [{ "PathRemovePrefix": "/claude" }]
      },
      "ClaudeApiKeyRoute": {
        "ClusterId": "ClaudeApiKeyCluster",
        "Match": { "Path": "/claude-api/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "CLAUDE_APIKEY" },
        "Transforms": [{ "PathRemovePrefix": "/claude-api" }]
      },
      "OpenAiAccountRoute": {
        "ClusterId": "OpenAiAccountCluster",
        "Match": { "Path": "/openai/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "OPENAI_ACCOUNT" },
        "Transforms": [{ "PathRemovePrefix": "/openai" }]
      },

      "AntigravityRoute": {
        "ClusterId": "AntigravityCluster",
        "Match": { "Path": "/antigravity/{**catch-all}" },
        "AuthorizationPolicy": "AiProxyPolicy",
        "Metadata": { "Platform": "ANTIGRAVITY" },
        "Transforms": [{ "PathRemovePrefix": "/antigravity" }]
      }
    },
    "Clusters": {
      "GeminiAccountCluster": {
        "Destinations": {
          "Default": { "Address": "https://cloudcode-pa.googleapis.com" }
        }
      },
      "GeminiApiKeyCluster": {
        "Destinations": {
          "Default": { "Address": "https://generativelanguage.googleapis.com" }
        }
      },
      "ClaudeAccountCluster": {
        "Destinations": {
          "Default": { "Address": "https://api.anthropic.com" }
        }
      },
      "ClaudeApiKeyCluster": {
        "Destinations": {
          "Default": { "Address": "https://api.anthropic.com" }
        }
      },
      "OpenAiAccountCluster": {
        "Destinations": {
          "Default": { "Address": "https://api.openai.com" }
        }
      },

      "AntigravityCluster": {
        "Destinations": {
          "Primary": { "Address": "https://generativelanguage.googleapis.com" }
        }
      }
    }
  }
}
```

### 新增文件：`AiRelay.Api/Transforms/AntigravityRequestTransform.cs`

```csharp
using System.Text;
using System.Text.Json.Nodes;
using AiRelay.Application.ProviderAccounts.AppServices;
using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.Services;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.Shared.Json;
using Yarp.ReverseProxy.Transforms;

namespace AiRelay.Api.Transforms;

public class AntigravityRequestTransform(
    IAccountTokenAppService accountTokenAppService,
    IModelMappingService modelMappingService,
    ILogger<AntigravityRequestTransform> logger)
    : DefaultRequestTransform(accountTokenAppService, ProviderPlatform.ANTIGRAVITY, logger)
{
    private const string AntigravityIdentity =
        "You are Antigravity, a powerful agentic AI coding assistant designed by the Google Deepmind team working on Advanced Agentic Coding.";

    protected override async Task ApplyProviderSpecificTransformAsync(
        RequestTransformContext context,
        AvailableAccountTokenOutputDto account)
    {
        // 确保目标端点正确
        await EnsureV1InternalEndpointAsync(context);

        // 转换请求体为 v1internal 格式
        await TransformToV1InternalAsync(context, account);
    }

    private Task EnsureV1InternalEndpointAsync(RequestTransformContext context)
    {
        var path = context.Path.Value;

        if (!path.StartsWith("/v1internal"))
        {
            var operation = "streamGenerateContent";

            if (path.Contains(":"))
            {
                var parts = path.Split(':');
                if (parts.Length > 1)
                {
                    operation = parts[1].Split('?')[0];
                }
            }

            context.Path = $"/v1internal:{operation}";

            if (operation == "streamGenerateContent")
            {
                context.Query = new QueryString("?alt=sse");
            }
        }

        return Task.CompletedTask;
    }

    private async Task TransformToV1InternalAsync(
        RequestTransformContext context,
        AvailableAccountTokenOutputDto account)
    {
        var request = context.HttpContext.Request;

        if (request.Body is null ||
            request.ContentLength == 0 ||
            request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) != true)
        {
            return;
        }

        using var reader = new StreamReader(request.Body, leaveOpen: false);
        var originalBody = await reader.ReadToEndAsync();

        if (JsonNode.Parse(originalBody) is not JsonObject originalJson)
        {
            Logger.LogWarning("无法解析请求体为 JSON 对象，跳过转换");
            return;
        }

        var v1InternalRequest = new JsonObject
        {
            ["project"] = account.ProjectId ?? "",
            ["requestId"] = $"agent-{Guid.NewGuid()}",
            ["userAgent"] = "antigravity",
            ["requestType"] = "agent"
        };

        // 提取并映射模型名称
        string modelId = "gemini-2.0-flash-exp";
        if (originalJson.TryGetPropertyValue("model", out var modelNode))
        {
            var requestedModel = modelNode?.GetValue<string>() ?? modelId;

            // 使用模型映射服务
            modelId = modelMappingService.GetMappedModel(
                ProviderPlatform.ANTIGRAVITY,
                requestedModel,
                account.ModelMappings);

            originalJson.Remove("model");
        }
        v1InternalRequest["model"] = modelId;

        // 注入 Antigravity 身份
        EnsureAntigravityIdentity(originalJson);

        v1InternalRequest["request"] = originalJson;

        var finalBody = v1InternalRequest.ToJsonString(JsonOptions.Compact);
        var bytes = Encoding.UTF8.GetBytes(finalBody);

        context.HttpContext.Request.Body = new MemoryStream(bytes);
        context.ProxyRequest.Content!.Headers.ContentLength = bytes.Length;

        Logger.LogDebug("已转换请求为 v1internal 格式，模型：{Model} (原始: {RequestedModel})",
            modelId,
            modelNode?.GetValue<string>());
    }

    private void EnsureAntigravityIdentity(JsonObject requestJson)
    {
        if (requestJson.TryGetPropertyValue("systemInstruction", out var sysInstNode) &&
            sysInstNode is JsonObject sysInstObj)
        {
            if (sysInstObj.TryGetPropertyValue("parts", out var partsNode) &&
                partsNode is JsonArray partsArray)
            {
                var hasIdentity = partsArray.Any(part =>
                    part?["text"]?.GetValue<string>()?.Contains("Antigravity") == true);

                if (!hasIdentity)
                {
                    partsArray.Insert(0, new JsonObject
                    {
                        ["text"] = AntigravityIdentity
                    });
                }
            }
            else
            {
                sysInstObj["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = AntigravityIdentity }
                };
            }
        }
        else
        {
            requestJson["systemInstruction"] = new JsonObject
            {
                ["parts"] = new JsonArray
                {
                    new JsonObject { ["text"] = AntigravityIdentity }
                }
            };
        }
    }
}
```

### 更新：`AiRelay.Api/Transforms/RequestTransformProvider.cs`

```csharp
public void Apply(TransformBuilderContext context)
{
    if (context.Route.Metadata?.TryGetValue(PlatformMetadataKey, out var platformValue) != true)
    {
        return;
    }

    if (!Enum.TryParse<ProviderPlatform>(platformValue, ignoreCase: true, out var platform))
    {
        return;
    }

    switch (platform)
    {
        case ProviderPlatform.GEMINI_ACCOUNT:
            AddTransform<GeminiAccountRequestTransform>(context);
            break;

        case ProviderPlatform.ANTIGRAVITY:
            AddTransform<AntigravityRequestTransform>(context);
            break;

        default:
            AddTransform(context, platform);
            break;
    }
}
```

---

## 2.6 模型测试 API（已存在，无需修改）

现有的 `ProviderAccountController.DebugModelAsync` 方法已通过 `ChatModelClientFactory` 支持所有平台，包括新增的 Antigravity，无需额外修改。

---

## 2.7 数据库迁移（可选）

如果 `AccountToken` 实体需要存储模型映射配置：

### 更新：`AiRelay.Domain/ProviderAccounts/Entities/AccountToken.cs`

```csharp
public class AccountToken : AuditedAggregateRoot
{
    // ... 现有字段 ...

    /// <summary>
    /// 模型映射配置（JSON 格式，账号级自定义）
    /// 例如: {"claude-sonnet-4-5": "claude-sonnet-4-5-20250514"}
    /// </summary>
    public string? ModelMappingsJson { get; set; }

    /// <summary>
    /// 模型映射字典（不映射到数据库）
    /// </summary>
    [NotMapped]
    public Dictionary<string, string>? ModelMappings
    {
        get => string.IsNullOrEmpty(ModelMappingsJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, string>>(ModelMappingsJson);
        set => ModelMappingsJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }
}
```

### EF Core 配置：

```csharp
builder.Property(e => e.ModelMappingsJson).HasColumnName("model_mappings");
builder.Ignore(e => e.ModelMappings);
```

---

# 三、架构设计说明

## 3.1 核心概念

### Antigravity 是什么？

Antigravity 是 Google 提供的统一 AI 代理层，具有以下特点：

1. **统一接口**：使用 Google 的 v1internal API 端点
2. **多模型支持**：同时支持 Gemini 和 Claude 模型
3. **协议转换**：在服务端自动完成 Claude ↔ Gemini 协议转换
4. **身份注入**：自动注入 Antigravity 身份提示词
5. **高可用性**：支持 URL 故障转移

### 为什么需要模型映射？

Antigravity 内部使用完整的模型版本号（如 `claude-sonnet-4-5-20250514`），而客户端通常使用简化名称（如 `claude-sonnet-4-5`）。模型映射服务负责：

- 版本号补全：`claude-sonnet-4-5` → `claude-sonnet-4-5-20250514`
- 别名解析：`gemini-flash` → `gemini-2.0-flash-exp`
- 账号级自定义：支持每个账号配置独立的映射规则

## 3.2 架构分层

```
┌─────────────────────────────────────────────────────────┐
│                     客户端应用                          │
│  (通过 API Key 调用 /antigravity/* 端点)                │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│              YARP ReverseProxy 层                       │
│  - AntigravityRequestTransform                         │
│  - 账号选择与负载均衡                                   │
│  - OAuth Token 替换                                     │
│  - v1internal 请求体封装                                │
│  - 模型名称映射                                         │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│            Google Generative Language API               │
│  - v1internal:streamGenerateContent?alt=sse            │
│  - 服务端协议转换 (Claude/Gemini)                       │
│  - 统一 SSE 响应格式                                    │
└─────────────────────────────────────────────────────────┘
```

## 3.3 数据流示例

### 客户端请求（Gemini 模型）

```http
POST /antigravity/v1beta/models/gemini-2.0-flash-exp:streamGenerateContent
Authorization: Bearer ak-xxx-your-api-key
Content-Type: application/json

{
  "contents": [
    {"role": "user", "parts": [{"text": "Hello"}]}
  ]
}
```

### Transform 后发送到 Google

```http
POST /v1internal:streamGenerateContent?alt=sse
Authorization: Bearer ya29.xxx-google-oauth-token
Content-Type: application/json

{
  "project": "project-12345",
  "requestId": "agent-uuid-xxx",
  "userAgent": "antigravity",
  "requestType": "agent",
  "model": "gemini-2.0-flash-exp",
  "request": {
    "contents": [
      {"role": "user", "parts": [{"text": "Hello"}]}
    ],
    "systemInstruction": {
      "parts": [
        {"text": "You are Antigravity, a powerful agentic AI coding assistant..."}
      ]
    }
  }
}
```

### 客户端请求（Claude 模型）

```http
POST /antigravity/v1/messages
Authorization: Bearer ak-xxx-your-api-key
Content-Type: application/json

{
  "model": "claude-sonnet-4-5",
  "messages": [
    {"role": "user", "content": "Hello"}
  ],
  "max_tokens": 100
}
```

### Transform 后（模型映射）

```http
POST /v1internal:streamGenerateContent?alt=sse
Authorization: Bearer ya29.xxx-google-oauth-token
Content-Type: application/json

{
  "project": "project-12345",
  "requestId": "agent-uuid-xxx",
  "userAgent": "antigravity",
  "requestType": "agent",
  "model": "claude-sonnet-4-5-20250514",  // ← 映射后的完整版本号
  "request": {
    "contents": [
      {"role": "user", "parts": [{"text": "Hello"}]}
    ],
    "systemInstruction": {
      "parts": [
        {"text": "You are Antigravity, a powerful agentic AI coding assistant..."}
      ]
    },
    "generationConfig": {
      "maxOutputTokens": 100
    }
  }
}
```

---

# 四、测试验证清单

## 前端验证

- [ ] 账号列表显示 Antigravity 图标（紫色火箭）
- [ ] 账号详情显示正确的平台标签 "Antigravity"
- [ ] 模型测试对话框支持 Antigravity 并显示 Gemini + Claude 模型列表
- [ ] 模型测试流式输出正常
- [ ] 订阅管理页面关联分组 Tooltip 显示紫色圆圈标记 🟣

## 后端验证

- [ ] Antigravity 账号可以成功创建
- [ ] 模型测试 API 返回正确的流式响应
- [ ] ReverseProxy `/antigravity/*` 路径正常转发
- [ ] 请求体成功转换为 v1internal 格式
- [ ] 模型映射逻辑正确：
  - [ ] `claude-sonnet-4-5` → `claude-sonnet-4-5-20250514`
  - [ ] `gemini-flash` → `gemini-2.0-flash-exp`
  - [ ] `gemini-1.5-pro` → 透传
- [ ] Antigravity 身份注入成功
- [ ] 日志记录完整（请求、映射、错误）
- [ ] URL Fallback 机制生效（主端点失败时切换到 daily）

## 集成测试

### 测试用例 1：Gemini 模型代理

```bash
curl -X POST http://localhost:5000/antigravity/v1beta/models/gemini-2.0-flash-exp:streamGenerateContent \
  -H "Authorization: Bearer ak-your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "contents": [
      {"role": "user", "parts": [{"text": "你好"}]}
    ]
  }'
```

**预期**：
- 返回 SSE 流式响应
- 响应内容包含中文回复
- 日志显示模型映射和 v1internal 转换

### 测试用例 2：Claude 模型代理（带模型映射）

```bash
curl -X POST http://localhost:5000/antigravity/v1/messages \
  -H "Authorization: Bearer ak-your-api-key" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "claude-sonnet-4-5",
    "messages": [
      {"role": "user", "content": "你好"}
    ],
    "max_tokens": 100
  }'
```

**预期**：
- 模型名称映射为 `claude-sonnet-4-5-20250514`
- 返回 SSE 流式响应
- 日志显示模型映射：`claude-sonnet-4-5` → `claude-sonnet-4-5-20250514`

### 测试用例 3：模型测试对话框

1. 前端创建 Antigravity 账号
2. 打开模型测试对话框
3. 选择 `Gemini 2.0 Flash (Experimental)` 模型
4. 输入测试消息
5. 观察流式输出

**预期**：
- 模型列表包含 Gemini 和 Claude 两类模型
- 流式输出实时显示
- 测试完成提示成功

---

# 五、常见问题 (FAQ)

## Q1: 为什么 Antigravity 只有一个枚举值，不区分 ACCOUNT/APIKEY？

**A**: Antigravity 本质上是一个统一的 OAuth 代理服务，所有访问都通过 Google OAuth 认证，不支持 API Key 方式。因此只需要一个枚举值 `ANTIGRAVITY`。

## Q2: 模型映射配置存储在哪里？

**A**: 模型映射有两级：
1. **全局映射**：硬编码在 `ModelMappingService` 中（常用映射规则）
2. **账号级映射**：存储在 `AccountToken.ModelMappingsJson` 字段（JSON 格式）

## Q3: 如果客户端请求的模型不在支持列表中会怎么样？

**A**: `ModelMappingService` 会依次尝试：
1. 账号级自定义映射
2. 直接支持检查
3. 全局映射
4. 前缀匹配
5. Gemini 模型透传（gemini- 开头）
6. 降级到默认模型 `gemini-2.0-flash-exp`

## Q4: ReverseProxy 和 ChatModelClient 有什么区别？

**A**:
- **ReverseProxy**：用于生产环境的 API 代理，客户端通过 API Key 调用
- **ChatModelClient**：用于模型测试功能，管理员在后台测试账号可用性

两者功能相似，但使用场景不同。

## Q5: Antigravity 身份注入的目的是什么？

**A**: 注入 Antigravity 身份提示词 (`You are Antigravity, a powerful agentic AI coding assistant...`) 是为了：
1. 统一模型行为（无论 Gemini 还是 Claude）
2. 告知模型当前处于 Antigravity 代理环境
3. 符合 Google 官方 Antigravity 实现规范

## Q6: 如何添加新的模型映射规则？

**A**: 修改 `ModelMappingService.AntigravityGlobalMappings` 字典：

```csharp
private static readonly Dictionary<string, string> AntigravityGlobalMappings = new()
{
    // 现有映射...

    // 新增映射
    ["custom-model-name"] = "actual-model-name-with-version"
};
```

---

# 六、后续优化建议

## 6.1 URL Fallback 优化（P1）

当前 URL fallback 逻辑在 `AntigravityChatModelClient` 中实现，建议：
- 在 `AntigravityRequestTransform` 中也实现相同逻辑
- 共享故障端点状态（使用分布式缓存）
- 添加自动恢复机制（定期探测主端点健康状态）

## 6.2 协议转换增强（P2）

当前仅支持基本的文本消息，后续可增强：
- Claude Messages API 完整转换（thinking blocks、tool calls）
- 图片消息支持（base64 → inlineData）
- 流式 thinking 输出

## 6.3 模型映射管理界面（P2）

在前端添加模型映射管理功能：
- 账号详情页添加"模型映射"标签
- 支持可视化编辑映射规则
- 提供映射测试功能

## 6.4 监控和告警（P1）

添加 Antigravity 专项监控：
- 端点可用性监控
- 模型调用成功率
- 模型映射命中率
- 异常模型请求告警

---

# 七、参考资料

- [Google Generative Language API 文档](https://ai.google.dev/api)
- [sub2api 项目 Antigravity 实现](../../.claude/skills/backend/internal/service/antigravity_gateway_service.go)
- [claude-relay-service Gemini OAuth 实现](../../.claude/skills/geminiAccountService.js)
- [YARP ReverseProxy 文档](https://microsoft.github.io/reverse-proxy/)

---

**文档版本**: v1.0
**创建日期**: 2026-01-28
**最后更新**: 2026-01-28
**作者**: AI Relay Team
