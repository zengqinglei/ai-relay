# 后端项目

AI-Relay 后端是一个基于 .NET 10.0 构建的应用程序，采用整洁架构（DDD）原则。它提供高性能的 AI 模型代理网关，支持 Claude、Gemini、OpenAI、Antigravity，具备多账户负载均衡和 OAuth 令牌管理功能。

---

## 架构

项目遵循整洁架构，职责清晰分离：

```
backend/
├── src/
│   ├── AiRelay.Api/              # 表现层（REST API）
│   ├── AiRelay.Application/      # 应用层（服务、初始化）
│   ├── AiRelay.Domain/           # 领域层（实体、领域服务）
│   └── AiRelay.Infrastructure/   # 基础设施层（EF Core、OAuth、Redis）
├── components/                    # 共享 Leistd 组件
└── ddd-struct/                    # DDD 基础类
```

---

## 环境准备

开始之前，请确保已安装：

- **.NET SDK**: 10.0+
- **PostgreSQL**: 15+（可选，未配置时使用内存存储）
- **Redis**: 7+（可选，未配置时使用内存缓存）

验证安装：
```bash
dotnet --version
```

---

## 快速开始

### 1. 克隆并导航

```bash
cd backend/src/AiRelay.Api
```

### 2. 配置应用

创建 `appsettings.Debug.json` 用于本地开发：

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=AiRelay_Debug;Username=postgres;Password=YourPassword",
    "Redis": "127.0.0.1:6379"
  },
  "Cors": {
    "AllowAnyLocalhost": true,
    "AllowedOrigins": []
  },
  "DefaultAdmin": {
    "Username": "admin",
    "Password": "Admin@123456",
    "Email": "admin@ai-relay.com",
    "Nickname": "系统管理员"
  },
  "Jwt": {
    "SecretKey": "YourSecretKey-MinimumLength32Characters!",
    "Issuer": "AiRelay",
    "Audience": "AiRelay"
  }
}
```

> **注意**：
> - `ConnectionStrings` 为可选配置。未配置时，应用使用内存存储。
> - Redis 连接字符串格式：`host:port,password=xxx,ssl=true`（密码和 SSL 可选）
> - 生产环境请修改 `DefaultAdmin.Password` 和 `Jwt.SecretKey`

### 3. 运行应用

```bash
dotnet run
```

应用将：
- 自动应用数据库迁移（如果配置了 PostgreSQL）
- 创建默认管理员用户
- 开始监听 `http://localhost:5240`

### 4. 访问 API

- **健康检查**: `http://localhost:5240/api/health`
- **Gemini Api Key 代理**: `http://localhost:5240/gemini-api/*`
- **Claude Api Key 代理**: `http://localhost:5240/claude-api/*`
- **OpenAI Api Key 代理**: `http://localhost:5240/openai-api/*`

---

## 环境配置

### 配置文件

- `appsettings.json` - 基础配置
- `appsettings.Debug.json` - 本地开发
- `appsettings.Development.json` - 开发环境
- `appsettings.Production.json` - 生产环境

### 主要配置项

#### 数据库（PostgreSQL）

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=AiRelay;Username=postgres;Password=YourPassword"
  }
}
```

或使用环境变量：
```bash
ConnectionStrings__Default="Host=localhost;Port=5432;Database=AiRelay;Username=postgres;Password=YourPassword"
```

#### Redis 缓存

```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,password=your-password,ssl=true"
  }
}
```

或使用环境变量：
```bash
ConnectionStrings__Redis="localhost:6379,password=your-password,ssl=true"
```

#### CORS

```json
{
  "Cors": {
    "AllowAnyLocalhost": true,
    "AllowedOrigins": ["https://example.com"]
  }
}
```

#### 默认管理员用户

```json
{
  "DefaultAdmin": {
    "Username": "admin",
    "Password": "Admin@123456",
    "Email": "admin@ai-relay.com",
    "Nickname": "系统管理员"
  }
}
```

#### JWT 认证

```json
{
  "Jwt": {
    "SecretKey": "YourSecretKey-MinimumLength32Characters!",
    "Issuer": "AiRelay",
    "Audience": "AiRelay"
  }
}
```

#### 外部 OAuth（可选）

```json
{
  "ExternalAuth": {
    "GitHub": {
      "ClientId": "your-github-client-id",
      "ClientSecret": "your-github-client-secret",
      "RedirectUri": "http://localhost:5240/api/external-auth/github/callback"
    },
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret",
      "RedirectUri": "http://localhost:5240/api/external-auth/google/callback"
    }
  }
}
```

---

## 数据库迁移

### 自动迁移（推荐）

应用启动时会自动应用待执行的迁移。无需手动操作。

### 手动迁移命令

**生成新迁移：**
```bash
dotnet ef migrations add MigrationName \
  --project src/AiRelay.Infrastructure \
  --startup-project src/AiRelay.Api \
  --output-dir Persistence/Migrations
```

**应用迁移：**

**方式一（推荐）：** 启动应用自动完成迁移

**方式二：** 手工完成迁移（默认使用appsettings.debug.json配置）
```bash
dotnet ef database update \
  --project src/AiRelay.Infrastructure \
  --startup-project src/AiRelay.Api
```

**使用自定义连接字符串应用迁移：**
```bash
dotnet ef database update \
  --project src/AiRelay.Infrastructure \
  --startup-project src/AiRelay.Api \
  --connection "Host=localhost;Database=AiRelay;Username=postgres;Password=postgres"
```

**移除最后一次迁移：**
```bash
dotnet ef migrations remove \
  --project src/AiRelay.Infrastructure \
  --startup-project src/AiRelay.Api
```

---

## 开发

### 构建

```bash
dotnet build
```

### 运行测试

```bash
dotnet test
```

### 使用特定环境运行

```bash
# Debug
dotnet run --environment Debug

# Development
dotnet run --environment Development

# Production
dotnet run --environment Production
```

### 监视模式（自动重载）

```bash
dotnet watch run
```

---

## Docker 部署

### 构建 Docker 镜像

从项目根目录：
```bash
docker build -t ai-relay:latest .
```

### 运行容器

```bash
docker run -d \
  --name ai-relay \
  -p 5240:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="Host=your-db;Database=AiRelay;Username=postgres;Password=YourPassword" \
  -e ConnectionStrings__Redis="your-redis:6379" \
  -e DefaultAdmin__Password="YourAdminPassword" \
  -e Jwt__SecretKey="YourSecretKey-MinimumLength32Characters!" \
  ai-relay:latest
```

---

## 核心功能

- **多提供商支持**：Gemini、Claude、OpenAI
- **多账户负载均衡**：基于使用量的智能账户选择
- **OAuth 令牌管理**：自动刷新访问令牌
- **API Key 认证**：加密存储，支持过期控制
- **使用量追踪**：分布式缓存 + 数据库审计日志
- **整洁架构**：DDD 职责清晰分离
- **自动数据库迁移**：启动时无缝更新架构

---

## 故障排查

### 数据库连接问题

如果遇到数据库连接错误：
1. 验证 PostgreSQL 是否运行
2. 检查 `appsettings.json` 中的连接字符串
3. 确保数据库存在或让应用自动创建

### Redis 连接问题

如果 Redis 不可用：
- 应用会自动回退到内存缓存
- 本地开发无需额外操作

### 端口已被占用

如果端口 5240 被占用：
```bash
# 在 launchSettings.json 中更改端口或使用环境变量
dotnet run --urls "http://localhost:5241"
```

---

## 其他资源

- **.NET 文档**: [官方文档](https://learn.microsoft.com/zh-cn/dotnet/)
- **Entity Framework Core**: [官方文档](https://learn.microsoft.com/zh-cn/ef/core/)
