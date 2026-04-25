# Workspace 聊天调度复用整体方案

> 日期：2026-04-24  
> 范围：模型测试入口 `AccountTokenController.DebugModelAsync`、代理入口 `SmartReverseProxyMiddleware.InvokeAsync`、聊天入口 `ChatSessionController.SendMessageAsync` 及其下游执行链路  
> 核心约束：工作区聊天的调度能力（同账号重试、切号重试、异常记录机制）应尽可能与代理入口保持一致

---

## 一、现状问题

### 1.1 三个入口共享了协议构造，但没有共享完整调度执行

当前三条链路都已建立到 `IChatModelHandler.CreateChatDownContext(ChatDownContextInput input)` 的统一协议构造能力：

- 模型测试入口：`backend/src/AiRelay.Api/Controllers/AccountTokenController.cs`
- 代理入口：`backend/src/AiRelay.Api/Middleware/SmartProxy/SmartReverseProxyMiddleware.cs`
- 聊天入口：`backend/src/AiRelay.Api/Controllers/ChatSessionController.cs`

这一步解决了“不同 provider 的请求体/路由构造重复维护”的问题，但仍未解决“调度执行能力分叉”的问题。

当前实际情况是：

- 代理入口拥有最完整的调度能力：选号、同账号重试、切号、并发槽位、指纹、错误分析、usage 异步记录。
- 模型测试入口仍是固定账号直连执行，没有和代理入口共用完整执行链。
- 聊天入口虽然通过 `WorkspaceChatExecutionAppService` 统一了工作区语义，但同账号重试、切号重试、异常记录与代理入口仍不是同一套核心实现。

### 1.2 `ISmartProxyAppService` 需要直接吸收聊天执行职责

现有代码中：

- `ISmartProxyAppService` 位于 `backend/src/AiRelay.Application/ProviderGroups/AppServices/ISmartProxyAppService.cs`
- `SmartProxyAppService` 位于 `backend/src/AiRelay.Application/ProviderGroups/AppServices/SmartProxyAppService.cs`

它已经承担了部分调度相关职责，例如：

- 选号
- 成功/失败状态回写
- 限流状态判断

如果聊天执行继续留在独立服务里，就会出现两套相邻但交叉的应用服务：

- 一套负责代理调度
- 一套负责聊天执行

这会造成：

- 命名混乱
- 调用边界不清晰
- 重试、切号、健康检查再次分叉

因此，本方案的唯一方向是：

- 不新增 `ISharedChatExecutionAppService`
- 原本计划放入共享聊天执行器的职责，全部并入 `ISmartProxyAppService`
- `WorkspaceChatExecutionAppService` 仅保留工作区聊天的输入组装能力，不再承担执行职责

### 1.3 控制器层 SSE 输出逻辑重复

以下两个控制器仍存在重复的 SSE 写回逻辑：

- `backend/src/AiRelay.Api/Controllers/AccountTokenController.cs`
- `backend/src/AiRelay.Api/Controllers/ChatSessionController.cs`

重复内容包括：

- 设置 SSE Header
- `StreamEvent` 序列化
- `data: ...\n\n` 输出
- 异常兜底为 error event
- 最后补 `[DONE]`

这部分属于 API 传输适配层，不应在多个 controller 中重复维护。

### 1.4 `SmartReverseProxyMiddleware` 过重，不利于聊天入口复用

`SmartReverseProxyMiddleware.InvokeAsync` 当前包含了大量可复用业务细节：

- 下游请求解析
- 资源池选号与排除账号
- 同账号重试与切号决策
- 并发槽位申请/等待/释放
- 首包健康检查
- 上游异常分析
- usage attempt 记录

如果聊天入口希望“尽可能与代理入口保持一致”，就必须复用这些能力。当前这些能力过多停留在 middleware 内部，导致聊天入口只能复制逻辑，而不是直接复用。

### 1.5 工作区聊天需要对齐代理入口的调度语义，而不是只对齐 handler 语义

聊天入口真正需要与代理入口保持一致的，不只是 handler 调用，而是完整调度语义：

- 资源池内选号
- 同账号重试
- 降级重试
- 切号重试
- 流健康检查
- 错误分类与失败决策
- usage 生命周期记录
- 最终命中账号/模型/状态回传

因此，这次方案优化的重点不是“再抽一个共享聊天执行器”，而是把三个入口的执行主链都收敛到 `ISmartProxyAppService`。

---

## 二、核心策略

### 2.1 总体策略

本次整体优化方案采用“三层收口”：

1. 协议构造统一到 `IChatModelHandler.CreateChatDownContext`
2. 调度执行统一到扩展后的 `ISmartProxyAppService`
3. SSE 写回统一到 API 层公共组件

这里的第二层不再单独新增 `ISharedChatExecutionAppService`，也不再引入新的统一执行服务命名，而是直接在现有 `ISmartProxyAppService` 上扩展职责并迁移到更合理的模块。

### 2.2 保留 `ISmartProxyAppService` 命名的原因

本轮先保留：

- `ISmartProxyAppService`
- `SmartProxyAppService`

原因：

- 现有代码、依赖注入、调用方已经大量使用这组命名，短期内重命名收益不高。
- 当前核心目标是“统一执行能力”，不是“统一术语体系”。
- 先把聊天、模型测试、代理三条链路收敛到一套服务，更符合第一阶段控风险的目标。
- 等职责稳定后，如仍认为 `SmartProxy` 命名语义过窄，再单独做一次低风险重命名。

### 2.3 服务合并原则

扩展后的 `ISmartProxyAppService` 统一承接以下职责：

- 资源池选号
- 固定账号执行
- 资源池模式执行
- 成功/失败状态回写
- 限流状态判断
- 账号可用性相关策略
- 同账号重试
- 降级重试
- 切号重试
- 并发槽位控制
- 指纹注入
- 首包健康检查
- usage 生命周期记录
- 统一执行结果输出

这样合并后，系统中只保留一套真正的“路由+执行”核心服务。

### 2.4 模块迁移原则

当前 `ISmartProxyAppService` 位于 `ProviderGroups/AppServices` 下，这更像历史产物，而不是最终合理归属。

合并后，建议迁移到独立模块：

- `backend/src/AiRelay.Application/ModelRouting/AppServices/`

原因：

- 它的职责已不再只是 provider group 相关。
- 它同时服务于代理、聊天、模型测试三个入口。
- 它是一个跨模块执行编排服务，独立模块更符合边界。

### 2.5 目标分层

#### A. Chat Request Mapper

职责：将业务对象映射为统一聊天输入。

调用方：

- 模型测试应用服务
- 工作区聊天应用服务

输入示例：

- `ChatSession`
- `ChatMessageInputDto`

输出：

- `ChatDownContextInput`

说明：

- 当前 `WorkspaceChatExecutionAppService.MapToChatDownContextInput` 保留手写映射是合理的。
- 若后续第二、第三个入口需要重复从业务对象组装 `ChatDownContextInput`，再抽成专用 `IChatRequestInputFactory`，不走通用 MasterMapper。

#### B. Smart Proxy Execution App Service

职责：统一“模型路由 + 执行 + 重试 + 切号 + 记录”的完整应用层编排。

核心职责包括：

- 根据执行模式决定是固定账号还是资源池选号
- 统一 handler 创建与上游调用
- 统一并发槽位获取与释放
- 统一指纹注入
- 统一同账号重试、降级、切号策略
- 统一首包健康检查
- 统一 usage/attempt 生命周期记录
- 输出标准化执行结果与 `StreamEvent` 流

说明：

- 这里的“统一”是指执行核心统一，不是入口适配统一。
- `ISmartProxyAppService` 负责“执行语义”，controller / middleware 仍各自保留自身的 HTTP 适配职责。

#### C. SSE Response Writer

职责：统一 API 层 SSE 输出。

建议命名：

- `ISseEventWriter`
- `SseEventWriter`

职责包括：

- 设置 SSE Header
- 序列化 `StreamEvent`
- 输出 `data:` 帧
- 异常转换为 error event
- 输出 `[DONE]`

### 2.6 三个入口的职责收敛

#### 2.6.1 模型测试入口

入口：`AccountTokenController.DebugModelAsync`

调整后职责：

- controller：只负责 SSE 输出
- `AccountTokenAppService`：组装单轮 `ChatDownContextInput`
- `ISmartProxyAppService`：走 `FixedAccount` 模式完成执行

说明：

- 模型测试不需要资源池选号，但应复用同一套单账号执行、健康检查、异常分类能力。

核心实现代码应调整为：

- `AccountTokenController.DebugModelAsync` 不再手写 SSE 循环，改为调用 `ISseEventWriter.WriteAsync(...)`
- `AccountTokenAppService.DebugModelAsync` 负责把 `ChatMessageInputDto` 组装为 `ExecuteModelRouteInput`
- `ISmartProxyAppService.ExecuteAsync(...)` 负责：
  - 加载指定账号
  - 调用 handler 构造上游请求
  - 执行单账号重试与首包健康检查
  - 返回标准化 `StreamEvent` 流

这一入口的关键差异：

- 输入来源是单条调试消息，不依赖聊天会话
- 执行模式固定为 `FixedAccount`
- 不需要资源池选号，不需要会话持久化
- 仍需要错误分析、流健康检查、usage 记录

#### 2.6.2 代理入口

入口：`SmartReverseProxyMiddleware.InvokeAsync`

调整后职责：

- middleware：保留认证上下文、`HttpContext -> DownRequestContext` 解析、原始响应转发与外围 usage worker 编排
- `ISmartProxyAppService`：负责调度策略与执行 attempt

说明：

- middleware 不再持有完整调度细节，只做 HTTP 入口协调。

核心实现代码应调整为：

- `SmartReverseProxyMiddleware.InvokeAsync` 负责：
  - 从 `HttpContext` 解析 `RouteProfile`、`ApiKeyId`、`UserId`
  - 构建 `DownRequestContext`
  - 记录请求级 usage start/end
  - 调用 `ISmartProxyAppService.ExecuteAsync(...)`
  - 将返回结果按代理协议写回下游响应
- `ISmartProxyAppService.ExecuteAsync(...)` 负责：
  - 基于 `ApiKey` / `ProviderGroup` 选号
  - 同账号重试、降级重试、切号重试
  - 并发槽位控制
  - 首包健康检查
  - 成功/失败状态回写
  - attempt 生命周期记录

这一入口的关键差异：

- 输入来源是原始 HTTP 请求体，不是聊天 DTO
- 输出目标是“原始代理响应字节流”，不是业务 `StreamEvent`
- 需要保留 `RouteProfile`、原始 header、原始 body、状态码透传语义
- 需要保留现有 usage worker 编排

#### 2.6.3 聊天入口

入口：`ChatSessionController.SendMessageAsync`

调整后职责：

- controller：只负责 SSE 输出
- `ChatSessionAppService`：负责用户消息落库、assistant 消息聚合与持久化
- `WorkspaceChatExecutionAppService`：收敛为“工作区上下文组装器”
- `ISmartProxyAppService`：负责与代理入口尽量一致的调度执行

说明：

- 聊天入口保留会话与消息语义，但不再持有独立调度逻辑。

核心实现代码应调整为：

- `ChatSessionController.SendMessageAsync` 不再手写 SSE 循环，改为调用 `ISseEventWriter.WriteAsync(...)`
- `ChatSessionAppService.SendMessageAsync` 负责：
  - 校验会话归属
  - 用户消息落库
  - 调用 `WorkspaceChatExecutionAppService` 组装 `ChatDownContextInput`
  - 组装 `ExecuteModelRouteInput`
  - 调用 `ISmartProxyAppService.ExecuteAsync(...)`
  - 聚合 assistant 回复并持久化
- `WorkspaceChatExecutionAppService` 仅负责：
  - 将 `ChatSession + 历史消息 + 当前输入` 映射为统一聊天执行输入

这一入口的关键差异：

- 输入来源是会话语义，不是裸 HTTP 请求
- 需要消息落库、assistant 聚合、标题生成等聊天业务语义
- 执行模式为 `ProviderGroup`
- usage 来源为 `WorkspaceChat`，且 `ApiKey` 可为空

### 2.7 三个入口共享哪些核心代码

为避免“看起来合并了，实际上只是接口名一样”，三个入口必须复用同一组核心方法，而不是各自复制逻辑。

建议 `ISmartProxyAppService` 内部显式收敛为以下主链：

1. `ExecuteAsync(ExecuteModelRouteInput input, CancellationToken cancellationToken)`
2. `ExecuteFixedAccountAsync(...)`
3. `ExecuteProviderGroupAsync(...)`
4. `ExecuteApiProxyAsync(...)`
5. `ExecuteAttemptAsync(...)`
6. `HandleSuccessStreamAsync(...)`
7. `DetermineFailureInstruction(...)`
8. `RecordAttemptAsync(...)`

其中复用边界应为：

- 三个入口共用：
  - handler 创建
  - 上游请求发送
  - 同账号重试
  - 切号重试
  - 降级重试
  - 并发槽位控制
  - 首包健康检查
  - usage / attempt 记录
  - 成功/失败状态回写

- 三个入口不共用：
  - 输入解析
  - controller/middleware 的 HTTP 输出方式
  - 聊天消息持久化
  - 代理原始响应透传

### 2.8 如何保障逻辑真正整合

仅仅“都调用 `ISmartProxyAppService`”还不够，必须从代码结构上避免再次分叉。

保障措施如下：

1. 三个入口禁止各自维护 `DetermineFailureInstruction(...)`
2. 三个入口禁止各自维护首包健康检查逻辑
3. 三个入口禁止各自维护同账号重试/切号重试循环
4. `SmartReverseProxyMiddleware` 中现有的执行主循环要下沉到 `ISmartProxyAppService`
5. `AccountTokenAppService` 与 `ChatSessionAppService` 只组装输入，不允许直接操作 handler 重试循环
6. 统一执行结果必须包含：
   - 最终命中账号
   - 最终命中模型
   - attempt 列表
   - usage
   - 最终错误
   - 输出事件流或原始字节流适配器

这样能保证：

- 失败策略只维护一份
- 调度能力只维护一份
- 入口层只保留自身必须存在的差异

### 2.9 调整后的树形目录结构

下面的树形结构中，已明确标记每个文件或目录的调整动作与具体内容：`[新增]`、`[调整]`、`[迁移]`、`[删除]`、`[保留]`。

```text
backend/
└─ src/
   ├─ AiRelay.Api/
   │  ├─ Controllers/
   │  │  ├─ AccountTokenController.cs [调整：改为通过 ISseEventWriter 输出；模型测试调用统一路由执行服务]
   │  │  └─ ChatSessionController.cs [调整：改为通过 ISseEventWriter 输出；聊天发送调用统一路由执行服务]
   │  │
   │  ├─ Infrastructure/ [新增目录]
   │  │  └─ Sse/ [新增目录]
   │  │     ├─ ISseEventWriter.cs [新增：抽取 SSE 输出接口]
   │  │     └─ SseEventWriter.cs [新增：统一 SSE Header、event、DONE 写回]
   │  │
   │  └─ Middleware/
   │     └─ SmartProxy/
   │        └─ SmartReverseProxyMiddleware.cs [调整：瘦身，只保留 HttpContext 适配、代理转发和外围编排]
   │
   ├─ AiRelay.Application/
   │  ├─ ModelRouting/ [新增目录：承接跨入口的统一调度执行模块]
   │  │  ├─ AppServices/
   │  │  │  ├─ ISmartProxyAppService.cs [迁移：从 ProviderGroups 迁入；扩展为聊天/模型测试/代理统一执行入口]
   │  │  │  └─ SmartProxyAppService.cs [迁移并调整：吸收原 SmartProxy 调度能力 + 聊天共享执行能力]
   │  │  │
   │  │  └─ Dtos/
   │  │     ├─ ExecuteModelRouteInput.cs [新增：统一执行输入，覆盖 FixedAccount / ProviderGroup / ApiProxy]
   │  │     ├─ ExecuteModelRouteMode.cs [新增：执行模式枚举]
   │  │     ├─ ExecuteModelRouteResult.cs [新增：统一执行结果]
   │  │     └─ ExecuteModelRouteAttemptResult.cs [新增：attempt 结果与失败轨迹]
   │  │
   │  ├─ ChatSessions/
   │  │  └─ AppServices/
   │  │     ├─ ChatSessionAppService.cs [调整：消息持久化保留，执行委托给 ISmartProxyAppService]
   │  │     ├─ IChatSessionAppService.cs [保留]
   │  │     ├─ WorkspaceChatExecutionAppService.cs [调整：缩减为工作区上下文组装器]
   │  │     └─ IWorkspaceChatExecutionAppService.cs [调整：接口职责收敛]
   │  │
   │  ├─ ProviderAccounts/
   │  │  └─ AppServices/
   │  │     └─ AccountTokenAppService.cs [调整：模型测试走 ISmartProxyAppService 的 FixedAccount 模式]
   │  │
   │  ├─ ProviderGroups/
   │  │  └─ AppServices/
   │  │     ├─ ISmartProxyAppService.cs [删除/迁移：迁移至 ModelRouting 模块，文件名保留]
   │  │     └─ SmartProxyAppService.cs [删除/迁移：迁移至 ModelRouting 模块，文件名保留]
   │  │
   │  └─ DependencyInjection.cs [调整：注册迁移后的 ISmartProxyAppService，并接入聊天/模型测试调用链]
   │
   ├─ AiRelay.Domain/
   │  └─ Shared/
   │     └─ ExternalServices/
   │        └─ ModelClient/
   │           ├─ IChatModelHandler.cs [已调整：统一 CreateChatDownContext]
   │           └─ Dto/
   │              └─ ChatDownContextInput.cs [已新增：统一聊天构造输入]
   │
   └─ AiRelay.Infrastructure/
      └─ Shared/
         └─ ExternalServices/
            └─ ModelClient/
               ├─ BaseChatModelHandler.cs [已调整：统一 CreateChatDownContext 抽象]
               ├─ OpenAiChatModelHandler.cs [已调整：协议构造统一]
               ├─ OpenAiCompatibleChatModelHandler.cs [已调整：协议构造统一]
               ├─ ClaudeChatModelHandler.cs [已调整：协议构造统一]
               ├─ GeminiApiChatModelHandler.cs [已调整：协议构造统一]
               ├─ GeminiAccountChatModelHandler.cs [已调整：协议构造统一]
               └─ AntigravityChatModelHandler.cs [已调整：协议构造统一]
```

### 2.10 核心代码策略

#### 2.10.1 统一执行输入模型

建议新增：

- `ExecuteModelRouteInput`

建议字段：

- `ExecuteModelRouteMode Mode`
- `Guid UserId`
- `Guid? ProviderGroupId`
- `Guid? FixedAccountId`
- `string ModelId`
- `string SessionId`
- `ChatDownContextInput ChatInput`
- `RouteProfile? RouteProfile`
- `UsageSource UsageSource`
- `string? DownRequestPath`
- `string? CorrelationId`
- `bool EnableRateLimitRetry`
- `bool EnableAccountSwitch`
- `bool EnableUsageLogging`
- `bool EnableStreamHealthCheck`

其中：

- `Mode = FixedAccount`：模型测试
- `Mode = ProviderGroup`：工作区聊天
- `Mode = ApiProxy`：代理入口

#### 2.10.2 统一执行结果模型

建议新增：

- `ExecuteModelRouteResult`
- `ExecuteModelRouteAttemptResult`

用于承载：

- 实际命中的账号
- 实际命中的 provider group
- 实际上游模型
- 最终状态
- 最终错误描述
- usage
- attempt 列表

#### 2.10.3 统一失败决策逻辑

应从 `SmartReverseProxyMiddleware` 收敛到 `SmartProxyAppService` 的逻辑包括：

- `DetermineFailureInstruction(...)`
- 同账号重试次数决策
- 降级重试级别提升
- 切号判定
- UnsupportedEndpoint 特判

这样才能真正保证：

- 工作区聊天与代理入口的失败策略一致
- 模型测试也不会单独维护一套临时异常逻辑

#### 2.10.4 统一成功流处理能力，但保留不同出口适配

建议抽出统一的：

- 首包健康检查
- `evt.HasOutput` 判定
- 流中断分类
- usage 收集

但保留两类输出适配：

1. 代理入口：字节转发适配
2. 聊天/模型测试入口：`StreamEvent` 业务事件适配

这样既能统一核心执行语义，又不会把代理的原始字节转发逻辑硬塞给聊天入口。

#### 2.10.5 usage 记录统一，但来源区分

#### 2.10.6 三个入口的输出适配策略

统一执行核心产出的不应直接是某个入口专属类型，而应是“统一执行结果 + 统一流事件”。

建议适配方式：

1. 模型测试入口：
   - `ISmartProxyAppService` 输出 `IAsyncEnumerable<StreamEvent>`
   - `ISseEventWriter` 负责写回 SSE

2. 聊天入口：
   - `ISmartProxyAppService` 输出 `IAsyncEnumerable<StreamEvent>`
   - `ChatSessionAppService` 在枚举过程中聚合 assistant 文本并落库
   - `ISseEventWriter` 负责写回 SSE

3. 代理入口：
   - `ISmartProxyAppService` 输出统一的流结果对象，内部携带原始字节或可转换字节
   - `SmartReverseProxyMiddleware` 只负责写 header 和 body

关键点：

- 统一的是“执行过程”，不是强迫三个入口使用完全相同的输出类型
- 代理入口继续保留原始字节透传语义
- 聊天与模型测试继续保留 `StreamEvent` 语义

建议 `SmartProxyAppService` 统一产出 usage 生命周期事件，来源区分如下：

- 代理入口：`UsageSource.ApiProxy`
- 聊天入口：`UsageSource.WorkspaceChat`
- 模型测试入口：`UsageSource.ModelTest`

这样：

- 记录结构统一
- 统计时可按来源筛选
- 聊天日志仍可保持 `ApiKey` 为空

---

## 三、实施计划

### 3.0 类 / 方法级改造清单

这一节直接约束到类、方法、调用链，避免后续实现时再次回到“概念统一、代码分叉”。

#### 3.0.1 `ISmartProxyAppService` 需要新增 / 调整的方法

文件：

- `backend/src/AiRelay.Application/ModelRouting/AppServices/ISmartProxyAppService.cs`
- `backend/src/AiRelay.Application/ModelRouting/AppServices/SmartProxyAppService.cs`

保留现有方法：

- `SelectAccountAsync(...)`
- `HandleSuccessAsync(...)`
- `HandleFailureAsync(...)`
- `IsRateLimitedAsync(...)`

新增核心入口方法：

- `Task<ExecuteModelRouteResult> ExecuteAsync(ExecuteModelRouteInput input, CancellationToken cancellationToken = default)`

新增内部拆分方法：

- `Task<ExecuteModelRouteResult> ExecuteFixedAccountAsync(ExecuteModelRouteInput input, CancellationToken cancellationToken)`
- `Task<ExecuteModelRouteResult> ExecuteProviderGroupAsync(ExecuteModelRouteInput input, CancellationToken cancellationToken)`
- `Task<ExecuteModelRouteResult> ExecuteApiProxyAsync(ExecuteModelRouteInput input, CancellationToken cancellationToken)`
- `Task<ExecuteModelRouteAttemptResult> ExecuteAttemptAsync(ExecuteAttemptContext context, CancellationToken cancellationToken)`
- `Task<StreamExecutionResult> HandleSuccessStreamAsync(StreamExecutionContext context, CancellationToken cancellationToken)`
- `Task<bool> TryAcquireConcurrencySlotAsync(SelectAccountResultDto selectResult, Guid activeRequestId, CancellationToken cancellationToken)`

下沉并转移进 `SmartProxyAppService` 的方法：

- `DetermineFailureInstruction(...)`
- `TryAcquireConcurrencySlotAsync(...)`
- `HandleSuccessResponseAsync(...)`
- `SetupFingerprintIfRequiredAsync(...)`

说明：

- 这些方法现在部分在 `SmartReverseProxyMiddleware` 中，后续必须迁移到 `SmartProxyAppService` 或其内部私有 helper。
- `SmartReverseProxyMiddleware` 不应再保留调度循环、重试循环、健康检查循环。

建议新增的内部上下文 / 结果对象：

- `ExecuteAttemptContext`
- `StreamExecutionContext`
- `StreamExecutionResult`
- `ProxyExecutionOutput`
- `ChatExecutionOutput`

目的：

- 避免 `ExecuteAsync(...)` 方法参数继续膨胀
- 明确“执行上下文”和“传输适配结果”是两个层次

#### 3.0.2 `SmartReverseProxyMiddleware` 需要删除 / 保留的代码块

文件：

- `backend/src/AiRelay.Api/Middleware/SmartProxy/SmartReverseProxyMiddleware.cs`

应保留：

- `ValidateAndGetContext(...)`
- `ProcessDownstreamRequestAsync(...)`
- `CaptureHeaders(...)`
- `WriteResponseHeaders(...)`
- 请求级 usage start / end 的 worker 编排
- API proxy 专属的 HTTP 响应写回

应删除并下沉到 `SmartProxyAppService`：

- 外层 `while (true)` 的切号循环
- 内层同账号重试循环
- `maxSameAccountRetries` 计算
- `degradationLevel` 控制
- `TryAcquireConcurrencySlotAsync(...)`
- `SetupFingerprintIfRequiredAsync(...)`
- `HandleSuccessResponseAsync(...)`
- `DetermineFailureInstruction(...)`
- 每次 attempt 的成功 / 失败分支决策

改造后的调用方式：

1. middleware 解析 `HttpContext`
2. middleware 组装 `ExecuteModelRouteInput`
3. middleware 调用 `smartProxyAppService.ExecuteAsync(...)`
4. middleware 只负责把结果写回 `HttpResponse`

#### 3.0.3 `AccountTokenAppService` / `AccountTokenController` 改造点

文件：

- `backend/src/AiRelay.Application/ProviderAccounts/AppServices/AccountTokenAppService.cs`
- `backend/src/AiRelay.Api/Controllers/AccountTokenController.cs`

`AccountTokenAppService.DebugModelAsync(...)` 应调整为：

1. 加载指定账号
2. 组装 `ChatDownContextInput`
3. 组装 `ExecuteModelRouteInput`
4. 调用 `ISmartProxyAppService.ExecuteAsync(...)`
5. 将结果转为 `IAsyncEnumerable<StreamEvent>`

`AccountTokenController.DebugModelAsync(...)` 应调整为：

1. 不再手写 SSE header + event 循环
2. 改为调用 `ISseEventWriter.WriteAsync(Response, eventStream, cancellationToken)`

不应再出现：

- controller 中直接 `JsonSerializer.Serialize(...)` 写 `data:`
- app service 中直接自己维护重试 / 健康检查 / handler 执行循环

#### 3.0.4 `ChatSessionAppService` / `WorkspaceChatExecutionAppService` / `ChatSessionController` 改造点

文件：

- `backend/src/AiRelay.Application/ChatSessions/AppServices/ChatSessionAppService.cs`
- `backend/src/AiRelay.Application/ChatSessions/AppServices/WorkspaceChatExecutionAppService.cs`
- `backend/src/AiRelay.Api/Controllers/ChatSessionController.cs`

`WorkspaceChatExecutionAppService` 只保留：

- `MapToChatDownContextInput(...)`
- 与工作区聊天有关的消息、模型、历史上下文组装

`WorkspaceChatExecutionAppService` 不再保留：

- 执行模型调用
- handler 调用循环
- 重试逻辑
- 切号逻辑
- usage 记录逻辑

`ChatSessionAppService.SendMessageAsync(...)` 应调整为：

1. 加载并校验会话
2. 写入用户消息
3. 读取聊天历史
4. 调用 `WorkspaceChatExecutionAppService.MapToChatDownContextInput(...)`
5. 组装 `ExecuteModelRouteInput`
6. 调用 `ISmartProxyAppService.ExecuteAsync(...)`
7. 边枚举 `StreamEvent` 边聚合 assistant 文本
8. 流结束后写入 assistant 消息
9. 如有必要更新会话标题 / 最后活动时间

`ChatSessionController.SendMessageAsync(...)` 应调整为：

1. 不再手写 SSE 输出
2. 改为 `ISseEventWriter.WriteAsync(...)`

聊天入口必须保留的差异能力：

- 消息落库
- assistant 文本聚合
- 会话标题更新
- usage source = `WorkspaceChat`
- `ApiKeyId` 可为空但 `UserId` 必须存在

#### 3.0.5 `ISseEventWriter` 需要提供的方法

文件：

- `backend/src/AiRelay.Api/Infrastructure/Sse/ISseEventWriter.cs`
- `backend/src/AiRelay.Api/Infrastructure/Sse/SseEventWriter.cs`

建议接口：

- `Task WriteAsync(HttpResponse response, IAsyncEnumerable<StreamEvent> events, CancellationToken cancellationToken = default)`
- `Task WriteErrorAsync(HttpResponse response, string message, CancellationToken cancellationToken = default)`

统一行为：

- 设置 SSE 所需 headers
- 序列化 `StreamEvent`
- 捕获异常并输出 error event
- 输出 `[DONE]`

这样可以统一：

- 模型测试 dialog
- 工作区聊天

#### 3.0.6 DTO / 输出模型改造点

文件建议位置：

- `backend/src/AiRelay.Application/ModelRouting/Dtos/`

建议新增：

- `ExecuteModelRouteInput`
- `ExecuteModelRouteMode`
- `ExecuteModelRouteResult`
- `ExecuteModelRouteAttemptResult`
- `ExecuteAttemptContext`
- `StreamExecutionContext`
- `StreamExecutionResult`

`ExecuteModelRouteInput` 最低应包含：

- `ExecuteModelRouteMode Mode`
- `Guid UserId`
- `Guid? ApiKeyId`
- `string? ApiKeyName`
- `Guid? ProviderGroupId`
- `Guid? FixedAccountId`
- `string SessionId`
- `string ModelId`
- `ChatDownContextInput? ChatInput`
- `DownRequestContext? DownRequestContext`
- `RouteProfile? RouteProfile`
- `UsageSource UsageSource`
- `bool EnableRateLimitRetry`
- `bool EnableAccountSwitch`
- `bool EnableStreamHealthCheck`

设计原则：

- 聊天 / 模型测试使用 `ChatInput`
- 代理入口使用 `DownRequestContext`
- 统一入口只接受一个 DTO，不接受几十个散参

#### 3.0.7 三个入口最终调用链

模型测试入口：

`AccountTokenController.DebugModelAsync`
-> `AccountTokenAppService.DebugModelAsync`
-> `ISmartProxyAppService.ExecuteAsync(Mode = FixedAccount)`
-> `ISseEventWriter.WriteAsync`

聊天入口：

`ChatSessionController.SendMessageAsync`
-> `ChatSessionAppService.SendMessageAsync`
-> `WorkspaceChatExecutionAppService.MapToChatDownContextInput`
-> `ISmartProxyAppService.ExecuteAsync(Mode = ProviderGroup)`
-> `ISseEventWriter.WriteAsync`

代理入口：

`SmartReverseProxyMiddleware.InvokeAsync`
-> `ProcessDownstreamRequestAsync`
-> `ISmartProxyAppService.ExecuteAsync(Mode = ApiProxy)`
-> `WriteResponseHeaders / Response.Body.WriteAsync`

#### 3.0.8 统一执行结果如何适配三种入口

建议 `ExecuteModelRouteResult` 至少包含：

- `Guid? AccountId`
- `string? AccountName`
- `Guid? ProviderGroupId`
- `string? UpModelId`
- `UsageStatus Status`
- `string? StatusDescription`
- `ResponseUsage? Usage`
- `IReadOnlyList<ExecuteModelRouteAttemptResult> Attempts`
- `IAsyncEnumerable<StreamEvent>? EventStream`
- `ProxyExecutionOutput? ProxyOutput`

适配原则：

- 模型测试 / 聊天使用 `EventStream`
- 代理入口使用 `ProxyOutput`
- 不强迫代理入口转换为 `StreamEvent`
- 不强迫聊天入口处理原始字节块

#### 3.0.9 为避免再次分叉，代码评审时必须检查的点

以下情况应视为偏离方案：

1. `SmartReverseProxyMiddleware` 中仍保留重试主循环
2. `ChatSessionAppService` 中直接创建 handler 并发送请求
3. `AccountTokenAppService` 中直接编排单账号流健康检查
4. controller 中仍手写 SSE 输出
5. 聊天入口与代理入口分别维护两套失败决策方法
6. usage / attempt 记录在三个入口中分别落地

只要出现上述任一情况，就说明“统一执行主链”没有真正落地

### 3.1 Phase 1：服务重命名与模块迁移

目标：

- 避免 `ISmartProxyAppService` 与新共享执行服务并存造成职责重叠
- 建立统一的 `ModelRouting` 模块，同时保留现有 `ISmartProxyAppService` 命名

任务：

1. 将 `ISmartProxyAppService` 迁移到 `ModelRouting/AppServices`
2. 将 `SmartProxyAppService` 迁移到 `ModelRouting/AppServices`
3. 扩展接口与实现，吸收聊天共享执行职责
4. 调整命名空间、依赖注入与调用方引用
5. 保持现有选号/限流/状态回写能力不回归

验收标准：

- 应用层中只保留一套统一执行服务
- 不再并列出现 `ISmartProxyAppService` 与 `ISharedChatExecutionAppService`

### 3.2 Phase 2：公共 SSE 传输层抽取

目标：

- 去掉 controller 中重复的 SSE 输出逻辑

任务：

1. 新增 `ISseEventWriter`、`SseEventWriter`
2. 改造 `AccountTokenController.DebugModelAsync`
3. 改造 `ChatSessionController.SendMessageAsync`
4. 统一 `[DONE]`、error event、异常日志策略

验收标准：

- 两个 controller 不再手写 SSE 循环
- 前端流式消费无回归

### 3.3 Phase 3：统一固定账号执行与资源池执行

目标：

- 让模型测试、聊天、代理三条链路共享同一套执行核心

任务：

1. 新增 `ExecuteModelRouteInput`、`ExecuteModelRouteResult` 等 DTO
2. 在 `SmartProxyAppService` 中建立三种模式：
   - `FixedAccount`
   - `ProviderGroup`
   - `ApiProxy`
3. 收敛以下逻辑：
   - handler 创建
   - 并发槽位申请/释放
   - 指纹注入
   - 单账号执行
   - 首包健康检查
   - retry policy 解析
   - 同账号重试/切号决策

验收标准：

- 模型测试走统一执行器的 `FixedAccount` 分支
- 代理入口走统一执行器的 `ApiProxy` 分支
- 工作区聊天可开始接入 `ProviderGroup` 分支

### 3.4 Phase 4：工作区聊天接入统一调度

目标：

- 工作区聊天的调度能力尽量与代理入口一致

任务：

1. 保留 `WorkspaceChatExecutionAppService` 的业务上下文组装职责
2. 保留 `MapToChatDownContextInput` 这类手写业务映射
3. 去掉其内部独立调度细节
4. 让 `ChatSessionAppService.SendMessageAsync` 继续负责：
   - 用户消息落库
   - assistant 内容聚合
   - assistant 消息持久化
5. 让 `ISmartProxyAppService` 负责：
   - 资源池选号
   - 同账号重试
   - 切号重试
   - 错误分析
   - usage 生命周期记录

验收标准：

- 聊天入口发生 429、5xx、空流、首包失败时，处理策略与代理入口尽量一致
- 聊天消息持久化逻辑不受影响

### 3.5 Phase 5：代理入口瘦身

目标：

- 让 `SmartReverseProxyMiddleware` 回归真正的 middleware 职责

任务：

1. 将可复用执行细节继续下沉到 `SmartProxyAppService`
2. middleware 只保留：
   - 认证上下文获取
   - `HttpContext -> DownRequestContext`
   - 原始响应头/字节转发
   - usage worker 外围编排
3. 保证 API Proxy 行为兼容现状

验收标准：

- middleware 代码量明显下降
- 核心调度策略只保留一份
- 聊天、模型测试、代理三条链路对齐失败策略

### 3.6 风险与注意事项

#### 风险 1：只改命名，不改职责边界

如果只是把 `ISmartProxyAppService` 保留下来但不真正吸收聊天共享执行能力，问题不会消失。

#### 风险 2：把聊天会话语义塞进统一路由执行服务

`SmartProxyAppService` 只负责调度执行，不负责：

- 会话持久化
- controller SSE 输出
- 聊天标题生成
- assistant 消息保存

#### 风险 3：过早把 `MapToChatDownContextInput` 抽成通用映射器

当前没有必要。它仍应保持手写，直到出现多个明确重复调用点。

---

## 四、结论

基于现有代码结构和你的补充约束，最佳方案不是新增一个并列的 `ISharedChatExecutionAppService`，也不是这一步再引入一个全新的服务命名，而是：

1. 直接扩展现有 `ISmartProxyAppService`，让它承接统一执行能力
2. 将其从 `ProviderGroups` 模块迁移到独立的 `ModelRouting` 模块
3. 让模型测试、工作区聊天、代理入口共同依赖这一套统一执行服务
4. 同时补齐 `SseEventWriter`，统一 API 层流式输出

这样既能保证工作区聊天的同账号重试、切号重试、异常记录机制尽可能与代理入口保持一致，也能避免应用层同时存在两套职责相邻的执行服务。 
