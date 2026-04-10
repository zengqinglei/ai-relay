<div align="center">

# AI-Relay

**🚀 现代化的 AI 大模型代理服务 | 多供应商统一接入、智能调度与精细化运营中心**

[![Container Image](https://img.shields.io/badge/ghcr.io-zengqinglei%2Fai--relay-181717?logo=github)](https://github.com/zengqinglei/ai-relay/pkgs/container/ai-relay)
[![License](https://img.shields.io/github/license/zengqinglei/ai-relay)](LICENSE)
[![GitHub Stars](https://img.shields.io/github/stars/zengqinglei/ai-relay)](https://github.com/zengqinglei/ai-relay/stargazers)
[![Docker](https://img.shields.io/badge/docker-ready-brightgreen.svg)](https://hub.docker.com/r/zengql/ai-relay/tags)

AI-Relay 通过 Angular 21 + .NET 10 + PostgreSQL + Redis 组合，实现 Claude/Gemini/OpenAI/Antigravity 兼容 API 代理、智能负载均衡、实时监控与自动化管理，帮助团队安全、可观测地管理多家 AI 服务商。

[在线预览 (Mock)](https://zengqinglei.github.io/ai-relay/#/auth/login) · [快速开始](#-快速开始) · [使用文档](#-开始使用)

</div>

---

## 📑 目录

<table>
<tr>
<td width="50%">

**快速导航**
- 🎯 [项目概述](#-项目概述)
- ⚡ [核心功能](#-核心功能)
- 🚀 [快速开始](#-快速开始)
- 🏗️ [架构说明](#️-架构说明)

</td>
<td width="50%">

**使用指南**
- 💻 [本地开发](#-本地开发)
- 📖 [开始使用](#-开始使用)
- ⚙️ [配置说明](#️-配置说明)
- ❓ [常见问题](#-常见问题)

</td>
</tr>
</table>

## 🎯 项目概述

AI-Relay 是一个现代化的主流 AI 大模型代理服务。它能提供多种智能代理能力，旨在帮助您在各种应用中更有效地使用大模型，目前已经支持的主流模型厂商如下：

- **Claude**（官方：OAuth、Api Key 模式：支持接入各种中转站）
- **Gemini**（官方：OAuth、Api Key 模式：官方以及各种中转站）
- **OpenAI**（官方：OAuth、Api Key 模式：官方以及各种中转站）
- **Antigravity**（官方反代）

**🌊 无缝瀑布流与弹性路由机制 (Waterfall Routing & Pools)**

当客户端将请求投递至网关时，网关不仅能够无感接管多种客户端（OpenClaw、Codex CLI、Gemini 官方插件），还可以实现极具弹性的调度：
- **同模混合 (Same-Model Pooling)**：为一个 API Key 绑定多个持有 Claude Opus 的账号分组。当一个账号遇到高并发、Rate Limit 或内部网络异常时，系统瞬间在资源池内重试或平滑切号，前端零感知。
- **跨模兜底 (Cross-Model Failover)**：允许你在订阅时，将优先级 1 设置为 Claude 资源池，优先级 2 设置为 OpenAI 资源池。当 Claude 全线崩溃或受限时，**Ai-Relay 会将 Anthropic API 请求即时无缝同构转化 (Protocol Translation) 为 OpenAI 协议**，直接调用备用池的 GPT-4 充当兜底，实现史无前例的降级容灾体验！

## ⚡ 核心功能

- 🌊 **瀑布流寻址与无缝容灾 (Waterfall Failover)**：独创式瀑布流路由引擎，API Key 可按优先级（Priority）无缝串联多个"资源池"：
  - **跨平台穿透降级**：当第一优先级资源池（如 Claude 池）触发限流、宕机或额度耗尽时，系统自动安全穿透至下一优先级备用池（如 OpenAI / Gemini 池）实施兜底。
  - **动态智能校验**：路由层具备“原子化检测”能力，对并发满载或异常的节点快速跳过，保障服务 99.99% 的高可用性。
- 🤖 **资源池内负载均衡**：支持资源池内部多种调度策略：
  - 动态自适应并发选号
  - 成本优先与加权随机
  - 多模态/长文本粘性会话 (Sticky Session)
- 🧩 **多平台合流代理**：零侵入式兼容接入 Claude、Gemini、OpenAI、Antigravity，底层差异在网关层完成统一平扫与协议同构。
- 🛡️ **资源池 (Pools) 抽象**：将单薄的 API Account 打包为富能力的“资源池”，支持混合模式（同平台多账号备用/跨平台多模型混编）。
- 📊 **实时观测台**：仪表盘详尽追踪 Token 过境情况、每小时耗时分布、错误率，实时掌握系统链路生命指征。
- 🔑 **下游订阅分发**：以 API Key 为粒度颁发通行证，提供额度管控、独立算账与时效强控。

## 🚀 快速开始

### 环境要求

- Docker 与 Docker Compose（推荐使用最新版本）
- PostgreSQL 14+：512+ MB, 0.2+ 核
- Redis 7+：64+ MB, 0.1+ 核
- 可选（本地开发）：Node.js 20+，.NET SDK 10

### 最低配置要求

- **CPU**：0.2+ 核
- **内存**：256+ MB
- **硬盘**：128+ MB

### 🐳 Docker Compose 部署（✨ 推荐方式）

Docker Compose 是**首选部署方式**，自动配置数据库、Redis 和应用服务，无需手动安装依赖，适合生产环境快速部署。

1. **下载配置文件**

   ```bash
   # 下载 docker-compose.yml
   wget https://raw.githubusercontent.com/zengqinglei/ai-relay/main/deploy/docker-compose.yml
   ```

2. **编辑环境变量**

   ```bash
   # 编辑配置文件
   nano docker-compose.yml
   ```

   **必须修改的配置项**：
   - `DefaultAdmin__Password`：管理员密码
   - `Jwt__SecretKey`：JWT 密钥（至少 32 字符）
   - `ConnectionStrings__Default`：PostgreSQL 连接字符串
   - `ConnectionStrings__Redis`：Redis 连接字符串（可选）

3. **启动服务**

   ```bash
   # 启动所有服务
   docker-compose up -d

   # 查看服务状态
   docker-compose ps

   # 查看日志
   docker-compose logs -f backend
   ```

4. **访问应用**

   - **管理后台**：`http://localhost:8080`
   - 使用配置的管理员账号登录

### 🐋 Docker 部署

```bash
docker run -d \
  --name ai-relay \
  -p 8080:8080 \
  -e TZ=Asia/Shanghai \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="Host=your-db-host;Database=AiRelay;Username=postgres;Password=YourPassword;GSS Encryption Mode=Disable" \
  -e ConnectionStrings__Redis="your-redis-host:6379,password=YourRedisPassword" \
  -e DefaultAdmin__Password="YourAdminPassword" \
  -e Jwt__SecretKey="YourSecretKey-MinimumLength32Characters!" \
  ghcr.io/zengqinglei/ai-relay:latest
```

## 🏗️ 架构说明

### 技术栈

**前端**
- Angular 21 + PrimeNG 21
- Tailwind CSS 4
- TypeScript

**后端**
- .NET 10 + ASP.NET Core
- Entity Framework Core
- PostgreSQL 15+
- Redis 7+

**部署**
- Docker + Docker Compose
- Kubernetes（可选）

### 高层架构

```
客户端 / CLI / 第三方系统
        │
        ▼
ASP.NET Core API (中继服务)
        │
        ├─ 认证与授权 (JWT)
        ├─ 账户管理与分组
        ├─ 智能调度引擎
        ├─ 熔断器与故障转移
        └─ 监控与统计
        │
多供应商 (Claude / Gemini / OpenAI / Antigravity) + PostgreSQL + Redis
```

### 核心组件

1. **认证层**：JWT Token 验证，支持自定义 API Key
2. **调度引擎**：根据权重、优先级、健康状态选择最佳供应商
3. **熔断保护**：自动检测故障并切换到备用供应商
4. **监控系统**：实时统计 Token 使用、成本与请求状态
5. **缓存层**：Redis 缓存热点数据，提升响应速度

## 💻 本地开发

本地开发环境搭建：

- **前端项目启动**：参考 [frontend/README.md](frontend/README.md)
- **后端项目启动**：参考 [backend/README.md](backend/README.md)

## 📖 开始使用

Ai-Relay 采用 **协议自动寻址 (Protocol Interception)** 机制。无论您的下游客户端使用何种协议（Anthropic / Google / OpenAI / Antigravity），Ai-Relay 都会自动拦截并根据您在“订阅管理”中配置的 **资源池瀑布流** 进行动态分发。

### 统一网关地址 (Base URL)
所有客户端统一指向：`http://your-server-ip:5240`

### Claude 客户端集成
**方式一**（推荐）：`~/.claude/settings.json`

```json
{
  "env": {
    "ANTHROPIC_AUTH_TOKEN": "sk-xxx...",
    "ANTHROPIC_BASE_URL": "http://localhost:5240"
  }
}
```

**方式二**：环境变量

```powershell
# Windows PowerShell
$env:ANTHROPIC_BASE_URL = "http://localhost:5240"
$env:ANTHROPIC_AUTH_TOKEN = "sk-xxx..."
```

```bash
# Linux/macOS
export ANTHROPIC_BASE_URL="http://localhost:5240"
export ANTHROPIC_AUTH_TOKEN="sk-xxx..."
```

### Gemini CLI

**方式一**（推荐，各操作系统通用）：`~/.gemini/.env`

账户 OAuth 方式，享受官方每日 1000 次免费配额：

```env
GEMINI_MODEL="gemini-3-pro-preview"
CODE_ASSIST_ENDPOINT="http://localhost:5240"
GOOGLE_CLOUD_ACCESS_TOKEN="sk-xxx..."
GOOGLE_GENAI_USE_GCA="true"
```

Api Key 方式：

```env
GEMINI_MODEL="gemini-3.1-pro-preview"
GOOGLE_GEMINI_BASE_URL="http://localhost:5240"
GEMINI_API_KEY="sk-xxx..."
```

### Codex CLI

在 `~/.codex/config.toml` 文件开头添加以下配置：

```toml
model_provider = "airelay"
model = "gpt-5.2"
model_reasoning_effort = "high"
disable_response_storage = true
preferred_auth_method = "apikey"

[model_providers.airelay]
name = "airelay"
base_url = "http://localhost:5240"  # 根据实际填写你服务器的 IP 地址或者域名
wire_api = "responses"
requires_openai_auth = true
```

在 `~/.codex/auth.json` 文件中配置 API 密钥：

```json
{
  "OPENAI_API_KEY": "订阅管理创建的 Api Key"
}
```

### OpenClaw

配置文件：`~/.openclaw/openclaw.json`

```json
{
  "models": {
    "mode": "merge",
    "providers": {
      "ai-relay-claude": {
        "baseUrl": "http://localhost:5240",
        "apiKey": "sk-xxx...",
        "api": "anthropic-messages",
        "models": [
          {
            "id": "claude-opus-4-6",
            "name": "claude-opus-4-6 (Custom Provider)",
            "reasoning": false,
            "input": ["text", "image"],
            "contextWindow": 200000
          }
        ]
      },
      "ai-relay-openai": {
        "baseUrl": "http://localhost:5240",
        "apiKey": "sk-xxx...",
        "api": "openai-completions",
        "models": [
          {
            "id": "gpt-5.4",
            "name": "gpt-5.4 (Custom Provider)",
            "reasoning": false,
            "input": ["text", "image"],
            "contextWindow": 128000
          }
        ]
      }
    }
  }
}
```

## ⚙️ 配置说明

### 核心环境变量

| 变量                          | 默认值      | 说明                                                      |
| ----------------------------- | ----------- | --------------------------------------------------------- |
| `ASPNETCORE_ENVIRONMENT`      | `Production` | 运行环境（Development/Production）                        |
| `DefaultAdmin__Password`      | -           | **必须修改**：管理员密码                                  |
| `Jwt__SecretKey`              | -           | **必须修改**：JWT 密钥（至少 32 字符）                    |
| `ConnectionStrings__Default`  | -           | PostgreSQL 连接字符串                                     |
| `ConnectionStrings__Redis`    | -           | Redis 连接字符串（可选，用于缓存和分布式锁）              |
| `Serilog__MinimumLevel__Default` | `Information` | 日志级别（Verbose/Debug/Information/Warning/Error）    |

### PostgreSQL 连接字符串格式

```bash
Host=localhost;Port=5432;Database=AiRelay;Username=postgres;Password=YourPassword;GSS Encryption Mode=Disable
```

### Redis 连接字符串格式

```bash
redis:6379,password=YourRedisPassword（可选）,,ssl=true（可选）,abortConnect=false（可选）
```

> **注意**：Redis 为可选配置，不配置时仅做**单机部署**系统将使用内存缓存和数据库锁。


## 🤝 贡献指南

欢迎通过 Issue / PR 参与开发！提交前请确保：

- 代码符合项目规范
- 提交信息遵循 Conventional Commits
- 已通过本地测试

## 📜 许可证

本项目采用 [MIT License](LICENSE)，可自由使用与二次开发。

