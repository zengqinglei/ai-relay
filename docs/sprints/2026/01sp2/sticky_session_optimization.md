# 粘性会话调度优化方案 (Sticky Session Optimization)

## 1. 背景与目标

### 1.1 问题现状
- **单点过载**: 原有的粘性会话策略基于 `ApiKey` 绑定 (`sticky:{apiKey}`), 导致同一 Key 的所有并发请求被路由到同一账号，造成单点过载。
- **CLI 支持缺失**: Gemini CLI / Claude Code 等工具不传递标准 Header，导致无法通过 Header 进行会话区分。
- **调度效率**: 原有策略在绑定账号不可用时可能缺乏快速的故障转移机制。

### 1.2 优化目标
- **会话级粘性**: 将绑定粒度下沉到会话 (Session) 级别，支持多并发负载均衡。
- **Cache First 策略**: 优先利用缓存，但遇到故障时立即失效并切换 ("Fail Fast, Switch Fast")。
- **多级标识提取**: 兼容各类客户端 (Header / Query / Body Fingerprinting)。

---

## 2. 核心架构设计

### 2.1 调度策略 (Cache First)
- **Hit (命中)**: 检查 `sticky:session:{groupId}:{platform}:{hash}`。
- **Valid (可用)**: 若账号状态正常 (`IsActive` & `IsAvailable`) -> **直接使用**。
- **Invalid (失效)**: 若账号不可用 -> **立即删除缓存 Key** -> **进入常规调度** -> **重新绑定新账号**。

### 2.2 标识提取策略 (Session Fingerprinting)
采用 **策略模式 (Strategy Pattern)** 针对不同平台实现差异化提取：

| 优先级 | 来源 | 说明 | 适用场景 |
| :--- | :--- | :--- | :--- |
| **High** | **Header** | `x-conversation-id`, `session-id`, `x-trace-id` | 标准 Web 客户端，高性能 (零开销) |
| **Med** | **Query** | `?session_id=xxx` | CLI 配置 URL 时手动指定 |
| **Low** | **Body** | `messages` / `contents` 哈希 | CLI 工具 (自动缓冲 Body 计算指纹) |

**技术细节**:
- 采用 `BaseSessionStickyStrategy` 封装通用逻辑。
- 对 Body 读取采用 `EnableBuffering` + `Stream Reset` 机制，确保不影响 YARP 转发。

---

## 3. 模块变更

### 3.1 Domain Layer (后端核心)
- **ProviderGroupDomainService**:
  - `SelectAccountForApiKeyAsync`: 增加 `sessionHash` 参数。
  - 实现 Cache First 逻辑：可用性检查 + 自动失效。
  - 缓存 Key 升级为: `sticky:session:{groupId}:{platform}:{sessionHash}`。

### 3.2 Application Layer (应用层)
- **AccountTokenAppService**:
  - `SelectAccountAsync`: 接收并透传 `sessionHash`。
  - **强制绑定**: 移除无分组时的全局兜底逻辑，强制校验 ApiKey 绑定关系。

### 3.3 Infrastructure / API Layer (基础设施)
- **SessionSticky 策略工厂**:
  - `ISessionStickyStrategy` / `BaseSessionStickyStrategy`
  - `OpenAiStickyStrategy`: 支持 `prompt_cache_key`, `messages` 哈希。
  - `GeminiStickyStrategy`: 支持 `contents` 哈希。
  - `ClaudeStickyStrategy`: 支持 `system` + `messages` 哈希。
- **YARP 集成**:
  - `DefaultRequestTransform`: 集成策略工厂，自动提取 Hash 并传递给 AppService。

### 3.4 Frontend (前端)
- **UI 提示**: 更新订阅管理弹窗文案，明确提示“未绑定分组将无法访问”。

---

## 4. 验证与测试

### 4.1 场景验证
1.  **标准 Web 请求**: 带 `x-conversation-id` -> 命中 Header 策略 -> 绑定成功。
2.  **CLI 请求**: 无 Header -> 命中 Body 策略 -> 计算 Content Hash -> 绑定成功。
3.  **故障转移**: 模拟绑定账号限流 -> 下次请求自动删除 Key -> 切换新账号。

### 4.2 性能影响
- **Header 命中**: 无额外开销。
- **Body 命中**: 增加一次内存缓冲和 SHA256 计算 (通常 < 1ms)，对大文件请求有轻微内存压力，但在可控范围内。
