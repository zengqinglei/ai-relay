# AI-Relay

<div align="center">

**现代化的 AI 大模型代理服务**

[![License](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![Docker](https://img.shields.io/badge/docker-ready-brightgreen.svg)](https://github.com/zengqinglei/ai-relay)

[在线演示](https://ai-demo.zengql.dpdns.org) · [快速开始](#如何部署) · [使用文档](#开始使用)

</div>

---

## 📑 目录

<table>
<tr>
<td width="50%">

**快速导航**
- 🎯 [项目概述](#项目概述)
- ⚡ [核心功能](#核心功能)
- 🚀 [如何部署](#如何部署)

</td>
<td width="50%">

**使用指南**
- 💻 [本地开发](#本地快速开始)
- 📖 [开始使用](#开始使用)
- 📄 [许可证](#许可证)

</td>
</tr>
</table>

## 项目概述

**在线演示 Demo**: https://ai-demo.zengql.dpdns.org （需 FQ 访问）
- 账号：`admin`
- 密码：`Admin@123456`

AI-Relay 项目是一个现代化的主流AI大模型代理服务。它能提供多种智能代理能力，旨在帮助您在各种应用中更有效地使用大模型，目前已经支持的主流模型厂商如下：

- **Claude**（官方：OAuth、Api Key模式：支持接入各种中转站）
- **Gemini**（官方：OAuth、Api Key模式：官方以及各种中转站）
- **OpenAI**（官方：OAuth、Api Key模型：官方以及各种中转站）
- **Antigravity**（官方反代）

## 核心功能

- **多账户管理**：可以添加各平台的多个账户
- **分组管理**：支持账户分组聚合智能调度策略
  - 自适应均衡
  - 加权随机
  - 优先级降级
  - 粘性会话
- **自定义Api Key**：可以为用户添加独立的Key
- **仪表盘**：支持统计Token使用情况查看

## 如何部署

### 配置要求（最低要求）

- **CPU**：0.2+ 核
- **内存**：256+ MB
- **硬盘**：128+ MB

### 部署方式

**Docker Compose**（推荐）

```bash
# 下载 docker-compose.yml
wget https://raw.githubusercontent.com/zengqinglei/ai-relay/main/deploy/docker-compose.yml

# 编辑环境变量
nano docker-compose.yml

# 启动服务
docker-compose up -d
```

完整配置参考 `deploy/docker-compose.yml`

**Docker**

```bash
docker run -d \
  --name ai-relay \
  -p 80:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__Default="Host=your-db-host;Database=AiRelay;Username=postgres;Password=YourPassword" \
  -e ConnectionStrings__Redis="your-redis-host:6379,password=YourRedisPassword" \
  -e DefaultAdmin__Password="YourAdminPassword" \
  -e Jwt__SecretKey="YourSecretKey-MinimumLength32Characters!" \
  ghcr.io/zengqinglei/ai-relay:latest
```

## 本地快速开始

本地开发环境搭建：

- **前端项目启动**：参考 [frontend/README.md](frontend/README.md)
- **后端项目启动**：参考 [backend/README.md](backend/README.md)

## 开始使用

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

账户OAuth方式，享受官方每日1000次免费配额：

```env
GEMINI_MODEL="gemini-3-pro-preview"
CODE_ASSIST_ENDPOINT="http://localhost:5240/gemini"
GOOGLE_CLOUD_ACCESS_TOKEN="sk-xxx..."
GOOGLE_GENAI_USE_GCA="true"
```

Api Key方式：

```env
GEMINI_MODEL="gemini-3.1-pro-preview"
GOOGLE_GEMINI_BASE_URL="http://localhost:5240/gemini-api"
GEMINI_API_KEY="sk-xxx..."
```

### Codex CLI

在 ~/.codex/config.toml 文件开头添加以下配置：

```env
model_provider = "airelay"
model = "gpt-5.2"
model_reasoning_effort = "high"
disable_response_storage = true
preferred_auth_method = "apikey"

[model_providers.airelay]
name = "airelay"
base_url = "http://localhost:5240/openai-key"  # 根据实际填写你服务器的ip地址或者域名
wire_api = "responses"
requires_openai_auth = true
```

在 ~/.codex/auth.json 文件中配置API密钥为 null：

```env
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

## 许可证

[MIT](LICENSE)
