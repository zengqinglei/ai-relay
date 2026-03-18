<div align="center">

# AI-Relay

**🚀 现代化的 AI 大模型代理服务 | 多供应商统一接入、智能调度与精细化运营中心**

[![Container Image](https://img.shields.io/badge/ghcr.io-zengqinglei%2Fai--relay-181717?logo=github)](https://github.com/zengqinglei/ai-relay/pkgs/container/ai-relay)
[![License](https://img.shields.io/github/license/zengqinglei/ai-relay)](LICENSE)
[![GitHub Stars](https://img.shields.io/github/stars/zengqinglei/ai-relay)](https://github.com/zengqinglei/ai-relay/stargazers)
[![Docker](https://img.shields.io/badge/docker-ready-brightgreen.svg)](https://github.com/zengqinglei/ai-relay)

AI-Relay 通过 Angular 21 + .NET 10 + PostgreSQL + Redis 组合，实现 Claude/Gemini/OpenAI/Antigravity 兼容 API 代理、智能负载均衡、实时监控与自动化管理，帮助团队安全、可观测地管理多家 AI 服务商。

[在线演示](https://ai-demo.zengql.dpdns.org/#/auth/login) · [快速开始](#-快速开始) · [使用文档](#-开始使用)

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

**在线演示 Demo**: https://ai-demo.zengql.dpdns.org （需 FQ 访问）
- 账号：`admin`
- 密码：`Admin@123456`

AI-Relay 是一个现代化的主流 AI 大模型代理服务。它能提供多种智能代理能力，旨在帮助您在各种应用中更有效地使用大模型，目前已经支持的主流模型厂商如下：

- **Claude**（官方：OAuth、Api Key 模式：支持接入各种中转站）
- **Gemini**（官方：OAuth、Api Key 模式：官方以及各种中转站）
- **OpenAI**（官方：OAuth、Api Key 模式：官方以及各种中转站）
- **Antigravity**（官方反代）

## ⚡ 核心功能

- 🤖 **智能负载均衡**：支持多种调度策略，内置熔断保护与故障转移，保障请求稳定。
  - 自适应均衡
  - 加权随机
  - 优先级降级
  - 粘性会话
- 🧩 **多账户管理**：同时接入 Claude、Gemini、OpenAI 等多个平台账户，统一管理。
- 🛡️ **分组管理**：支持账户分组聚合，灵活配置调度策略。
- 📊 **实时监控**：仪表盘统计 Token 使用情况，实时掌控运行态势。
- 🔑 **自定义 API Key**：为用户创建独立的 API Key，支持精细化权限控制。

## 🚀 快速开始

### 环境要求

- Docker 与 Docker Compose（推荐使用最新版本）
- 可选（本地开发）：Node.js ≥ 20，.NET SDK 10

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

### Claude CLI

**方式一**（推荐，各操作系统通用）：`~/.claude/settings.json`

```json
{
  "env": {
    "ANTHROPIC_AUTH_TOKEN": "sk-xxx...",
    "ANTHROPIC_BASE_URL": "http://localhost:5240/claude-api"
  }
}
```

**方式二**：环境变量

```powershell
# Windows PowerShell
$env:ANTHROPIC_BASE_URL = "http://localhost:5240/claude-api"
$env:ANTHROPIC_AUTH_TOKEN = "sk-xxx..."
```

```bash
# Linux/macOS
export ANTHROPIC_BASE_URL="http://localhost:5240/claude-api"
export ANTHROPIC_AUTH_TOKEN="sk-xxx..."
```

### Gemini CLI

**方式一**（推荐，各操作系统通用）：`~/.gemini/.env`

账户 OAuth 方式，享受官方每日 1000 次免费配额：

```env
GEMINI_MODEL="gemini-3-pro-preview"
CODE_ASSIST_ENDPOINT="http://localhost:5240/gemini"
GOOGLE_CLOUD_ACCESS_TOKEN="sk-xxx..."
GOOGLE_GENAI_USE_GCA="true"
```

Api Key 方式：

```env
GEMINI_MODEL="gemini-3.1-pro-preview"
GOOGLE_GEMINI_BASE_URL="http://localhost:5240/gemini-api"
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
base_url = "http://localhost:5240/openai-key"  # 根据实际填写你服务器的 IP 地址或者域名
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
        "baseUrl": "http://localhost:5240/claude-api/v1",
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
        "baseUrl": "http://localhost:5240/openai-api/v1",
        "apiKey": "sk-xxx...",
        "api": "openai-completions",
        "models": [
          {
            "id": "gpt-5.2",
            "name": "gpt-5.2 (Custom Provider)",
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

