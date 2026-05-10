# 默认 ApiKey 按需创建与默认模型接口方案

## 需求

### 背景

当前系统已经具备用户注册、ApiKey 创建、默认供应商分组初始化和 OAuth2 accessToken 认证能力。

本次目标是在客户端需要默认模型配置时，按需确保当前用户存在默认 ApiKey，并返回默认 provider/model 信息。

### 目标

1. 提供受 `[Authorize]` 保护的接口：

```http
GET /api/v1/api-keys/default/default-models
```

2. 客户端通过 OAuth2 登录后的 `accessToken` 调用接口。
3. 接口幂等确保当前用户存在名为 `default` 的默认 ApiKey。
4. 如果默认 ApiKey 不存在，接口自动创建并绑定默认供应商分组 `default`。
5. 接口返回当前用户默认 ApiKey 和默认 provider/model 信息。
6. 接口返回结构保持通用，不固定适配 OpenClaw 或其他特定客户端格式。
7. 不在用户注册链路中创建默认 ApiKey，避免注册流程附带额外副作用。

### 默认模型约束

接口仅提供两类模型协议：

- `anthropic-messages`
- `openai-completions`

接口仅提供以下模型：

- `qwen3.5-plus`
- `glm-5`
- `glm-5.1`
- `minimax-m2.5`
- `minimax-m2.7`

## 核心策略

### 1. 按需幂等创建默认 ApiKey

默认 ApiKey 不再通过用户注册领域事件创建，而是在客户端调用默认模型接口时按需确保存在。

这样可以覆盖更多场景：

- 普通注册用户首次调用。
- 第三方 OAuth2 首次登录用户调用。
- 历史用户调用。
- 用户删除默认 ApiKey 后再次调用。

核心流程：

```text
GET /api/v1/api-keys/default/default-models
└── ApiKeyAppService.GetDefaultProviderModelsAsync
    ├── EnsureDefaultApiKeyAsync
    │   ├── 查询当前用户 name = "default" 的 ApiKey
    │   ├── 已存在：直接返回
    │   ├── 不存在：获取分布式锁 api-key:default:{userId}
    │   ├── 锁内二次查询默认 ApiKey
    │   ├── 仍不存在：EnsureDefaultProviderGroupAsync
    │   ├── 查询默认供应商分组
    │   └── ApiKeyDomainService.CreateWithKeyAsync(..., [(1, defaultGroup.Id)])
    └── 返回默认 ApiKey 和默认 provider/model 信息
```

### 2. 现有链路复用

#### ApiKey 创建链路

```text
ApiKeyController.CreateAsync
└── ApiKeyAppService.CreateAsync
    ├── EnsureBindingsAccessibleAsync
    └── ApiKeyDomainService.CreateWithKeyAsync
        ├── 校验同用户下 ApiKey 名称唯一
        ├── 生成或接收自定义 secret
        ├── 计算 secret hash
        ├── 校验 secret hash 唯一
        ├── 加密 secret
        ├── new ApiKey(...)
        ├── AddBinding(priority, providerGroupId)
        └── apiKeyRepository.InsertAsync(...)
```

默认 ApiKey 创建复用 `ApiKeyDomainService.CreateWithKeyAsync`，避免重复实现密钥生成、加密、hash 校验和绑定逻辑。

#### 默认分组初始化链路

```text
SystemInitializer.InitializeAsync
├── InitializeRolesAsync
├── ProviderGroupDomainService.EnsureDefaultProviderGroupAsync
│   ├── 查询 IsDefault = true 或 Name = "default" 的分组
│   ├── 若存在但状态不规范，调用 MarkAsDefault("default") 修正
│   └── 若不存在，创建 name = "default"、isDefault = true 的 ProviderGroup
├── InitializeDefaultAdminAsync
└── InitializeOpenIddictAsync
```

接口兜底创建逻辑复用 `ProviderGroupDomainService.EnsureDefaultProviderGroupAsync`。

### 3. 调整内容树形目录结构

```text
backend/src
├── AiRelay.Application
│   ├── ApiKeys
│   │   ├── AppServices
│   │   │   ├── ApiKeyAppService.cs                  # 默认 ApiKey 确保与默认模型接口实现
│   │   │   └── IApiKeyAppService.cs                 # GetDefaultProviderModelsAsync
│   │   ├── Dtos
│   │   │   └── DefaultProviderModelsOutputDto.cs     # 默认模型接口响应 DTO
│   │   └── Options
│   │       └── DefaultProviderModelsOptions.cs       # 默认 provider/model 配置
└── AiRelay.Api
    ├── Controllers
    │   └── ApiKeyController.cs                       # 暴露 default/default-models 接口
    ├── Program.cs                                    # 注册 DefaultProviderModelsOptions
    └── appsettings.json                              # 配置 DefaultProviderModels
```

### 4. provider id 前缀收敛策略

已审视当前项目已有 `ai-relay` 相关定义：

- `ai-relay-api`：OpenIddict resource 名称，语义是 OAuth/API 资源，不适合作为客户端 provider id 前缀。
- `admin@ai-relay.com`：默认管理员邮箱域名，不适合作为配置命名来源。
- `AiRelay`：项目/命名空间/缓存前缀等 PascalCase 名称，不适合作为客户端 provider id。

因此新增配置 `DefaultProviderModels:ProviderIdPrefix`，默认值为 `ai-relay`，接口据此生成：

- `ai-relay-anthropic`
- `ai-relay-completions`

### 5. 核心实现代码

#### DefaultProviderModelsOptions

```csharp
namespace AiRelay.Application.ApiKeys.Options;

public class DefaultProviderModelsOptions
{
    public const string SectionName = "DefaultProviderModels";

    public string ProviderIdPrefix { get; set; } = "ai-relay";

    public string[] Models { get; set; } =
    [
        "qwen3.5-plus",
        "glm-5",
        "glm-5.1",
        "minimax-m2.5",
        "minimax-m2.7"
    ];
}
```

#### DefaultProviderModelsOutputDto

```csharp
public class DefaultProviderModelsOutputDto
{
    public required DefaultApiKeyOutputDto ApiKey { get; init; }
    public required IReadOnlyList<DefaultProviderModelEndpointOutputDto> Endpoints { get; init; }
}

public class DefaultApiKeyOutputDto
{
    public required string Name { get; init; }
    public required string Secret { get; init; }
}

public class DefaultProviderModelEndpointOutputDto
{
    public required string Id { get; init; }
    public required string Protocol { get; init; }
    public required string BaseUrl { get; init; }
    public IReadOnlyList<string> Models { get; init; } = [];
}
```

#### ApiKeyController 接口

```csharp
[HttpGet("default/default-models")]
public async Task<DefaultProviderModelsOutputDto> GetDefaultProviderModelsAsync(CancellationToken cancellationToken)
{
    var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
    return await apiKeyAppService.GetDefaultProviderModelsAsync(baseUrl, cancellationToken);
}
```

#### ApiKeyAppService 默认 ApiKey 确保逻辑

```csharp
private async Task<ApiKey> EnsureDefaultApiKeyAsync(CancellationToken cancellationToken)
{
    const string defaultApiKeyName = "default";
    var userId = currentUser.Id!.Value;
    var existingApiKey = await apiKeyRepository.GetFirstAsync(
        x => x.UserId == userId && x.Name == defaultApiKeyName,
        cancellationToken: cancellationToken);
    if (existingApiKey != null)
    {
        return existingApiKey;
    }

    await using var handle = await distributedLock.LockAsync($"api-key:default:{userId}", cancellationToken);

    existingApiKey = await apiKeyRepository.GetFirstAsync(
        x => x.UserId == userId && x.Name == defaultApiKeyName,
        cancellationToken: cancellationToken);
    if (existingApiKey != null)
    {
        return existingApiKey;
    }

    await providerGroupDomainService.EnsureDefaultProviderGroupAsync(cancellationToken);

    var defaultGroup = await providerGroupRepository.GetFirstAsync(x => x.IsDefault, cancellationToken: cancellationToken);
    if (defaultGroup == null)
    {
        throw new BadRequestException("默认供应商分组不存在");
    }

    return await apiKeyDomainService.CreateWithKeyAsync(
        userId,
        defaultApiKeyName,
        "系统自动创建的默认 API Key",
        null,
        null,
        [(1, defaultGroup.Id)],
        cancellationToken);
}
```

#### ApiKeyAppService 默认模型输出

```csharp
private static IReadOnlyList<DefaultProviderModelEndpointOutputDto> BuildDefaultProviderModelEndpoints(
    DefaultProviderModelsOptions options,
    string baseUrl)
{
    return
    [
        new DefaultProviderModelEndpointOutputDto
        {
            Id = $"{options.ProviderIdPrefix}-anthropic",
            Protocol = "anthropic-messages",
            BaseUrl = baseUrl,
            Models = options.Models
        },
        new DefaultProviderModelEndpointOutputDto
        {
            Id = $"{options.ProviderIdPrefix}-completions",
            Protocol = "openai-completions",
            BaseUrl = baseUrl,
            Models = options.Models
        }
    ];
}
```

### 6. appsettings.json 配置

```json
"DefaultProviderModels": {
  "ProviderIdPrefix": "ai-relay",
  "Models": [
    "qwen3.5-plus",
    "glm-5",
    "glm-5.1",
    "minimax-m2.5",
    "minimax-m2.7"
  ]
}
```

`PublicBaseUrl` 不再配置，接口从当前 HTTP 请求地址生成 `baseUrl` 并固定拼接 `/v1`。

## 实施计划

### 阶段一：清理注册事件方案

1. 移除 `UserRegisteredEvent`。
2. 移除 `User` 实体中的注册事件发布。
3. 移除 `UserRegisteredEventHandler`。
4. 移除应用层 DI 中的注册事件处理器绑定。

### 阶段二：默认模型接口

1. 新增 `DefaultProviderModelsOptions`。
2. 新增 `DefaultProviderModelsOutputDto`。
3. 在 `IApiKeyAppService` 增加 `GetDefaultProviderModelsAsync`。
4. 在 `ApiKeyAppService` 中实现：
   - 确保当前用户存在默认 ApiKey。
   - 使用分布式锁保护首次并发创建。
   - 返回 `ai-relay-anthropic` 和 `ai-relay-completions` 两个 endpoint。
   - `baseUrl` 使用请求根地址拼接 `/v1`。
   - 模型列表来自 `DefaultProviderModels:Models`。
5. 在 `ApiKeyController` 增加：

```http
GET /api/v1/api-keys/default/default-models
```

### 阶段三：验证

1. 后端构建通过。
2. `git diff --check` 无空白错误。
3. 文档同步为按需幂等创建默认 ApiKey 的方案。
4. 验证未登录访问返回 401。
5. 验证已登录访问返回当前用户默认 ApiKey 和默认模型列表。
