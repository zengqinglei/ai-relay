# OAuth2/OIDC 授权服务器方案

## 背景与目标

AiRelay 需要作为标准 OAuth2/OpenID Connect 授权服务器，对内支撑自有 Web 前端登录，对外支持后续桌面、CLI、移动端或第三方客户端接入。

当前目标是统一到标准授权模型：浏览器类公开客户端使用 Authorization Code + PKCE，后端统一由 OpenIddict 签发和验证 token，模型代理 API Key 鉴权保持不变。

## 授权标准

采用 **OpenIddict** 作为内嵌 OAuth2/OIDC 授权服务器。

当前标准形态：

- 支持 Authorization Code Flow + PKCE。
- 支持 OpenID Connect Discovery。
- 支持 UserInfo Endpoint。
- 服务端保留 Refresh Token Flow 能力，用于后续非 Web SPA 客户端。
- 不支持 password grant。
- 不支持 implicit flow。
- 不再使用 `register`、`external_login` 等自定义 token grant。

## 当前端点

| 端点 | 方法 | 用途 |
|---|---|---|
| `/.well-known/openid-configuration` | GET | OIDC Discovery |
| `/connect/authorize` | GET/POST | Authorization Code + PKCE 授权端点 |
| `/connect/token` | POST | authorization code / refresh token 换 token |
| `/connect/userinfo` | GET/POST | 获取 OIDC 用户信息 |
| `/connect/logout` | GET/POST | OIDC end-session 登出端点 |
| `/api/v1/auth/session-login` | POST | 自有 Web 登录页建立授权服务器 Cookie 登录态 |
| `/api/v1/auth/register` | POST | 用户注册，不直接签发 token |
| `/api/v1/external-auth/{provider}/login-url` | GET | 获取 GitHub/Google 外部登录 URL |
| `/api/v1/external-auth/{provider}/callback` | POST | 外部登录后建立授权服务器 Cookie 登录态 |

## 内置 Web 客户端

系统初始化内置 Web 客户端：

```text
client_id: ai-relay-web
display_name: AiRelay Web
client_type: public
require_pkce: true
```

用途：AiRelay 自有 Web 前端。

授权范围：

```text
openid profile email roles
```

该客户端不授予 refresh token 权限，不请求 `offline_access`。

重定向地址：

```text
https://localhost:5243/auth/callback
http://localhost:5240/auth/callback
```

登出后重定向地址：

```text
https://localhost:5243/auth/login
http://localhost:5240/auth/login
```

初始化时会确保内置 Web 客户端使用当前标准客户端标识。

## 后续客户端策略

`ai-relay-web` 只代表自有 Web 前端。后续客户端应独立注册 client：

| 客户端类型 | 建议 client_id | 客户端类型 | Refresh Token |
|---|---|---|---|
| Web 前端 | `ai-relay-web` | public + PKCE | 不授予 |
| 桌面客户端 | `ai-relay-desktop` | public + PKCE | 可授予 |
| CLI 客户端 | `ai-relay-cli` | public + PKCE / device flow | 可授予 |
| 移动端 | `ai-relay-mobile` | public + PKCE | 可授予 |
| 服务端集成 | 按系统命名 | confidential | 可授予 |

Refresh token 只应授予确有长会话需求且具备安全存储能力的客户端。

## 登录与登出流程

### 登录

1. 前端跳转 `/connect/authorize`，携带 `client_id=ai-relay-web`、`response_type=code`、`code_challenge`。
2. 授权服务器发现未登录时重定向到 `/auth/login?returnUrl=...`。
3. 登录页调用 `/api/v1/auth/session-login` 校验账号密码。
4. 后端写入 `AiRelayCookie`。
5. 前端继续回到授权流程。
6. 授权服务器返回 authorization code。
7. 前端调用 `/connect/token`，使用 `code_verifier` 换取 access token。

### 登出

1. 前端清理本地 access token。
2. 跳转 `/connect/logout?client_id=ai-relay-web&post_logout_redirect_uri=...`。
3. 后端清理 `AiRelayCookie`。
4. OpenIddict 完成 end-session，并跳回登录页。

## Claims 与用户信息

统一由 `IAuthPrincipalFactory` 创建 ClaimsPrincipal。

关键 claims：

| 含义 | Claim |
|---|---|
| 用户 ID | `sub`、`ClaimTypes.NameIdentifier` |
| 用户名 | `name`、`preferred_username`、`ClaimTypes.Name` |
| 昵称 | `given_name`、`ClaimTypes.GivenName` |
| 邮箱 | `email`、`ClaimTypes.Email` |
| 角色 | `role`、`ClaimTypes.Role` |
| 头像 | `picture` |

`/connect/userinfo` 根据授权 scope 返回对应字段。

## 部署要求

生产环境要求：

- 关闭 OpenIddict development certificate。
- 配置正式 signing certificate 与 encryption certificate。
- 每个客户端配置精确 redirect URI 和 post logout redirect URI。
- Nginx 代理必须传递 forwarded headers：

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-Host $host;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
```

后端已启用 `X-Forwarded-For`、`X-Forwarded-Proto`、`X-Forwarded-Host` 处理，用于保证 OAuth/OIDC 外部地址、scheme 和 redirect 行为正确。

## 当前状态

已完成：

- 引入 OpenIddict Server / Validation / EF Core stores。
- 新增 OpenIddict 协议表迁移。
- 启用 Authorization Code + PKCE。
- 保留服务端 Refresh Token Flow 能力。
- 移除 password grant、implicit flow、自定义 token grant。
- 内置客户端改为 `ai-relay-web`。
- `ai-relay-web` 不授予 refresh token。
- 新增 `/connect/logout` 标准登出链路。
- 注册、外部登录不再直接签发旧 JWT。
- 删除旧 JWT token provider。
- 保留模型代理 API Key 鉴权。
- 支持同源开发 SPA 代理。
- 支持 Nginx forwarded headers。
- 前端与 mock 已同步 OAuth Code + PKCE 登录流程。

后续增强：

- 客户端注册/管理 UI。
- 非 Web 客户端 refresh token 生命周期、轮换、撤销和审计策略。
- BFF/session cookie 模式评估，降低浏览器端 bearer token 暴露面。
- 生产证书部署与轮换流程。
