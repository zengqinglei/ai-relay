# Claude & Antigravity 清洗逻辑深度对比报告

## 1. 核心结论

经过代码审计，针对您提出的“Antigravity 和 Claude 都有清洗逻辑”的疑问，结论如下：

*   **Antigravity 清洗逻辑**: 主要是 **JSON Schema 降级**（为了兼容性）。这部分逻辑在 `sub2api` 中非常完善，但在当前项目 `ai-relay` 中 **完全缺失**。这是您需要优化的重点。
*   **Claude 清洗逻辑**: 主要是 **协议合规与特性降级**（为了成功率）。这部分逻辑在 `sub2api` 和 `ai-relay` 中 **高度一致**，`ai-relay` 已经移植了绝大部分核心功能，仅缺失个别边缘特性。

---

## 2. 详细对比分析

### 2.1. Antigravity 清洗逻辑 (JSON Schema Cleaning)

**目的**: 将客户端（如 Cursor）发出的复杂 `tools` 定义（JSON Schema Draft 2020-12），转换为 Gemini/Antigravity 模型能理解的简单格式。

| 功能点 | sub2api (Go) / Antigravity-Manager (Rust) | ai-relay (C#) | 状态 |
| :--- | :--- | :--- | :--- |
| **$ref 展开** | ✅ 递归展开嵌套的 `$defs` 和 `definitions` | ❌ 无 | **缺失** |
| **联合类型降级** | ✅ 智能合并 `anyOf`/`oneOf`/`allOf` 分支 | ❌ 无 | **缺失** |
| **类型修正** | ✅ `["string", "null"]` -> `"string"`, Enum -> String | ❌ 无 | **缺失** |
| **约束迁移** | ✅ 移除 `pattern`/`minItems` 等并将约束写入 `description` | ❌ 无 | **缺失** |
| **兜底策略** | ✅ 自动补全空 Object 的 properties 和 required | ❌ 无 | **缺失** |

**影响**: 缺失此逻辑会导致复杂 Agent 任务（如代码库搜索）在 Antigravity 渠道上失败（报 400 错误）。

### 2.2. Claude 清洗逻辑 (Protocol & Feature Cleaning)

**目的**: 确保请求符合 Claude API 的限制（如 Cache Control 数量），并在账号权限不足（如 OAuth 账号缺签名）时自动降级以保证对话继续。

| 功能点 | sub2api (Go) | ai-relay (C#) | 状态 |
| :--- | :--- | :--- | :--- |
| **Cache Control 限制** | ✅ 强制限制最多 4 个 ephemeral 块，超限自动移除 | ✅ `EnforceCacheControlLimit` 已实现 | **已对齐** |
| **非法字段清理** | ✅ 移除 Thinking 块中的 `cache_control` | ✅ `RemoveCacheControlFromThinkingBlocks` 已实现 | **已对齐** |
| **OAuth 提示词注入** | ✅ 自动注入 Claude Code System Prompt | ✅ `InjectClaudeCodePrompt` 已实现 | **已对齐** |
| **Thinking 降级** | ✅ 签名错误时将 Thinking 块转为 Text (Level 1) | ✅ `FilterThinkingBlocks` 已实现 | **已对齐** |
| **Tools 降级** | ✅ 严重错误时移除 Tools 定义 (Level 2) | ✅ `FilterSignatureSensitiveBlocks` 已实现 | **已对齐** |
| **System 前缀过滤** | ✅ `filterSystemBlocksByPrefix` (移除计费元数据等) | ❌ 无 | **差异 (低优)** |

**结论**: `ai-relay` 的 `ClaudeChatModelClient.cs` 已经非常成熟，无需大的改动。

---

## 3. 建议行动项

基于上述分析，我们的优化重心应完全集中在 **Antigravity** 上。

1.  **首要任务**: 移植 Antigravity 的 Schema 清洗逻辑。
    *   在 `AiRelay.Domain.Shared` 中创建 `JsonSchemaCleaner` 类。
    *   实现 `$ref` 展开、联合类型合并、约束迁移等核心算法。
    *   在 `AntigravityChatModelClient` 和 `GeminiApiChatModelClient` 中调用此清洗器。

2.  **次要任务 (可选)**: 补充 Claude 的 System 前缀过滤。
    *   如果您的业务场景需要在 System Prompt 中注入会被上游拒绝的敏感元数据，可以考虑补充 `filterSystemBlocksByPrefix` 逻辑。否则，当前实现已足够健壮。
