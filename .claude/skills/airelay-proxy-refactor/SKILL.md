---
name: airelay-proxy-refactor
description: 分析和重构 AiRelay 代理转发、模型测试、调度和使用记录链路
---

# 使用场景

涉及以下对象或主题时使用：

- `SmartReverseProxyMiddleware.InvokeAsync`
- `AccountTokenAppService.DebugModelAsync`
- `IChatModelClient`
- `BaseChatModelHandler` / `XxxChatModelHandler`
- `XxxRequestProcessor` / `XxxResponseProcessor`
- Claude / Gemini / OpenAI / Antigravity 转发链路
- OAuth / API Key / Account 认证方式
- SSE、非流式、同步/异步转换
- sessionHash、粘性会话、调度、并发控制、重试、降级、限流
- UsageRecord、Token、费用统计、上下游日志记录

# 固定前置检查

1. 阅读 `docs/standards/code_standard/backend_develop.md`。
2. 阅读代理入口完整链路，不要只看单个文件。
3. 同时审视代理入口和模型测试入口。
4. 如用户要求参考项目，对比 `sub2api`、`antigravity-manager`、`claude-relay-service`、`claude-code-hub` 中对应路由的完整链路。
5. 如涉及 .NET HttpClient、YARP、SSE、流式传输、官方 SDK 或协议细节，查阅最新官方文档。

# 固定分析链路

## 1. 入口校验

- ApiKey 获取与验证
- Claims / Route metadata
- ApiKeyName
- RouteProfile 或 ProviderPlatform

## 2. 下游请求解析

- Url
- Query
- Header
- UserAgent
- Body 读取方式
- sessionHash 提取
- modelId 提取
- request body preview
- 图片、文件、大 JSON、SSE 场景的内存和性能影响

## 3. 账号选择

- ApiKey 与分组绑定
- 粘性会话
- 最大并发数
- 调度策略
- 限流状态
- 失败重试
- fallback / 降级
- 跳过失败账号

## 4. 上游请求转换

- BaseUrl 和 fallback url
- Path / Query 转换
- Header 白名单、覆盖、伪装官方客户端
- 认证信息注入
- ModelId 映射
- RequestBody 清洗、增强、提示词注入、projectId 注入
- thoughtSignature / signature 处理
- Antigravity 特殊转换

## 5. 请求转发

- 是否保留 YARP 能力
- 是否动态切换上游地址
- 是否避免重复读写 Body
- 是否支持 HTTP/2、SSE、长推理空闲心跳

## 6. 响应处理

- ResponseHeader
- SSE event 解析
- 非流式 JSON 解析
- 错误响应解析
- Token 输入/输出/缓存提取
- 真实 modelId 提取
- thought/signature 提取或降级移除
- ResponseBody 转换
- 下游错误感知

## 7. 使用记录

- 上下游 Url / Header / Body preview
- Token
- 金额
- 状态
- 错误信息
- 耗时
- 分组、账号、ApiKey、请求路由、请求 IP、UserAgent

# 设计原则

- 避免上下游请求 Body、响应 Body 多次读取。
- SSE 场景优先边读边转发，避免完整缓存响应。
- 图片、文件、大内容只提取必要信息，不做整包日志。
- 不通过 `context.Items` 随意传递核心业务状态，优先使用明确上下文对象。
- 保持职责单一，但不要过度拆分接口和服务。
- Api 层保留 HTTP 细节，Application 协调，Domain 放核心调度和业务规则，Infrastructure 实现外部调用。
- 改造前后必须对比两个入口和所有平台认证分支，避免遗漏。

# 输出格式

## 需求理解
## 当前完整链路
## 现状问题
## 参考项目对比结论
## 核心策略
## 调整内容树形目录结构
## 核心接口与代码设计
## 改造前后行为一致性清单
## 实施计划
## 性能与风险评估
## 验证方式
