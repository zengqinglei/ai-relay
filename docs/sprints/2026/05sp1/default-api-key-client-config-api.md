# 默认 ApiKey 默认模型接口文档

## 接口概览

该接口用于第三方客户端在用户完成 OAuth2 登录后，使用当前用户的 `accessToken` 获取默认 ApiKey 以及默认 provider/model 信息。

接口返回的是通用结构，不绑定 OpenClaw、Claude Code、OpenAI SDK 或其他客户端的专有配置格式。客户端可根据返回内容自行转换为目标配置文件。

当前接口仅提供两类模型协议：

- `anthropic-messages`
- `openai-completions`

当前接口仅提供以下模型：

- `qwen3.5-plus`
- `glm-5`
- `glm-5.1`
- `minimax-m2.5`
- `minimax-m2.7`

## 命名收敛说明

已审视当前项目中已有的 `ai-relay` 相关定义：

- `ai-relay-api`：OpenIddict resource 名称，语义是 OAuth/API 资源，不适合作为客户端 provider id 前缀。
- `admin@ai-relay.com`：默认管理员邮箱域名，不适合作为配置命名来源。
- `AiRelay`：项目/命名空间/缓存前缀等 PascalCase 名称，不适合作为客户端展示 id。

因此接口 provider id 前缀统一收敛到配置项：

```json
"DefaultProviderModels": {
  "ProviderIdPrefix": "ai-relay"
}
```

默认返回：

- `ai-relay-anthropic`
- `ai-relay-completions`

## 访问前提

1. 用户已完成系统登录。
2. 客户端已获取当前用户的 OAuth2 `accessToken`。
3. 请求头中携带：

```http
Authorization: Bearer {accessToken}
```

未携带有效 Token 时接口返回 `401 Unauthorized`。

## 获取默认模型配置

### 请求

```http
GET /api/v1/api-keys/default/default-models
Authorization: Bearer {accessToken}
```

### cURL 示例

```bash
curl -X GET "http://localhost:5240/api/v1/api-keys/default/default-models" \
  -H "Authorization: Bearer ${ACCESS_TOKEN}" \
  -H "Accept: application/json"
```

### BaseUrl 来源

响应中的 `baseUrl` 不再从配置文件读取，而是从当前 HTTP 请求地址动态生成并固定拼接 `/v1`：

```text
{Request.Scheme}://{Request.Host}{Request.PathBase}/v1
```

因此生产环境应确保反向代理正确传递：

- `X-Forwarded-Proto`
- `X-Forwarded-Host`

项目已配置 `ForwardedHeadersOptions` 读取转发头。

### 行为说明

接口会按以下规则返回当前用户的默认 ApiKey：

1. 查询当前认证用户名为 `default` 的 ApiKey。
2. 如果已存在，直接返回该 ApiKey 的明文 secret 与默认模型信息。
3. 如果不存在，系统会自动确保默认供应商分组 `default` 存在，并为当前用户创建一个名为 `default` 的 ApiKey。
4. 新创建的默认 ApiKey 会绑定默认供应商分组。
5. 接口只返回当前认证用户自己的 ApiKey，不支持通过用户 ID 查询他人 ApiKey。

## 响应结构

### 200 OK

```json
{
  "apiKey": {
    "name": "default",
    "secret": "sk-xxxxxxxxxxxxxxxxxxxx"
  },
  "endpoints": [
    {
      "id": "ai-relay-completions",
      "protocol": "openai-completions",
      "baseUrl": "http://localhost:5240/v1",
      "models": [
        "qwen3.5-plus",
        "glm-5",
        "glm-5.1",
        "minimax-m2.5",
        "minimax-m2.7"
      ]
    },
    {
      "id": "ai-relay-anthropic",
      "protocol": "anthropic-messages",
      "baseUrl": "http://localhost:5240/v1",
      "models": [
        "qwen3.5-plus",
        "glm-5",
        "glm-5.1",
        "minimax-m2.5",
        "minimax-m2.7"
      ]
    }
  ]
}
```

## 字段说明

### apiKey

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `name` | `string` | 默认 ApiKey 名称，当前固定为 `default`。 |
| `secret` | `string` | 当前用户默认 ApiKey 明文 secret。客户端后续请求 AI Relay 代理接口时使用该值。 |

### endpoints

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| `id` | `string` | provider id，默认由 `DefaultProviderModels:ProviderIdPrefix` 加协议后缀生成。 |
| `protocol` | `string` | 模型协议标识，当前仅返回 `anthropic-messages`、`openai-completions`。 |
| `baseUrl` | `string` | AI Relay 对外访问基础地址，由当前请求地址拼接 `/v1` 生成。 |
| `models` | `string[]` | 当前 provider 允许客户端展示和使用的模型列表。 |

## 客户端使用方式

客户端获取响应后，可按目标客户端格式自行映射。

### 通用映射建议

- API Key：使用 `apiKey.secret`。
- Base URL：使用目标 endpoint 的 `baseUrl`，该值已包含 `/v1`。
- 模型列表：仅使用目标 endpoint 的 `models`。

### 端点选择建议

| 客户端目标 | 推荐筛选条件 |
| --- | --- |
| Anthropic Messages 兼容客户端 | `id = "ai-relay-anthropic"` 或 `protocol = "anthropic-messages"` |
| OpenAI Chat Completions 兼容客户端 | `id = "ai-relay-completions"` 或 `protocol = "openai-completions"` |

## 错误响应

### 401 Unauthorized

未登录或 `accessToken` 无效。

```json
{
  "code": 401,
  "message": "未授权"
}
```

### 400 Bad Request

默认供应商分组不可用等业务异常。

```json
{
  "code": 400,
  "message": "默认供应商分组不存在"
}
```

## 配置项

接口返回内容受 `appsettings.json` 中 `DefaultProviderModels` 配置影响：

```json
{
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
}
```

### 配置说明

| 字段 | 说明 |
| --- | --- |
| `ProviderIdPrefix` | provider id 前缀，默认 `ai-relay`。 |
| `Models` | 返回给客户端的模型白名单，当前应仅包含 `qwen-3.5`、`qwen-3.6`、`glm-5`、`glm-5.1`、`minimax-m2.5`、`minimax-m2.7`。 |

## 安全注意事项

1. `apiKey.secret` 是明文密钥，客户端应安全存储。
2. 服务端不得在日志中输出 `apiKey.secret`。
3. 接口仅允许返回当前认证用户的默认 ApiKey。
4. 生产环境应正确配置反向代理转发头，避免 `baseUrl` 生成错误。
5. 如果用户主动删除默认 ApiKey，当前接口会按“确保存在”的语义重新创建默认 ApiKey。
