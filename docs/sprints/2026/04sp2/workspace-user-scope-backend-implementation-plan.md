# Workspace 用户域后端改造详细方案与实施计划

> 日期：2026-04-21  
> 范围：`/workspace` 仪表盘、聊天、我的订阅、使用日志，以及与 `/platform` 的接口复用  
> 依据：已阅读前端 `/workspace` 相关页面、service、`_mock/api`、`_mock/data`，并抽样阅读后端一个完整 `POST -> 应用服务 -> 领域服务 -> 仓储 -> EF 配置` 链路

---

## 1. 目标与约束

本次后端改造目标不是单独为 `/workspace` 再做一套平行系统，而是在现有 `/platform` 能力上补齐“当前用户视角”的数据归属、查询隔离、聊天会话持久化和复用式执行链路，使普通用户进入 `/workspace` 后看到的数据与自己拥有的 `ApiKey`、聊天会话、订阅、使用记录严格一致。

约束如下：

- `/platform` 与 `/workspace` 尽量复用相同领域模型、仓储和统计接口，不重复造轮子。
- 数据隔离由后端根据当前请求 Token 中的用户身份完成，前端不承担隔离职责。
- 工作区聊天属于用户私有数据。
- 聊天产生使用记录时，使用日志页面中的 `API KEY` 字段为空。
- 聊天接口前端交互参考现有“模型测试 dialog”的流式模式。
- 附件需要后端持久化。

---

## 2. 已确认现状

### 2.1 前端业务现状

已阅读以下页面与服务：

- `frontend/src/app/features/workspace/components/dashboard/workspace-dashboard.ts`
- `frontend/src/app/features/workspace/components/chat/workspace-chat.ts`
- `frontend/src/app/features/workspace/components/my-subscriptions/workspace-my-subscriptions.ts`
- `frontend/src/app/features/workspace/components/usage-logs/workspace-usage-logs.ts`
- `frontend/src/app/features/workspace/services/chat-session-service.ts`
- `frontend/src/app/features/workspace/services/dashboard-service.ts`
- `frontend/src/app/features/workspace/services/subscription-service.ts`
- `frontend/src/app/features/workspace/services/usage-record-service.ts`
- `frontend/src/app/features/workspace/services/usage-record-metric-service.ts`

当前 `/workspace` 的核心视角已经是“普通用户”，但仍依赖 mock 层做用户隔离，后端真实接口尚未统一补齐。

### 2.2 Mock 现状

已阅读以下 mock：

- `frontend/_mock/api/workspace-chat.ts`
- `frontend/_mock/api/subscription.ts`
- `frontend/_mock/api/usage-records.ts`
- `frontend/_mock/api/usage-record-metric.ts`
- `frontend/_mock/data/workspace-chat.ts`
- `frontend/_mock/data/subscriptions.ts`
- `frontend/_mock/data/usage-record.ts`
- `frontend/_mock/utils/current-user.ts`

结论：

- mock 已经开始按 `userId` 做会话、订阅、使用记录隔离。
- mock 层已经表达出后端所需的真实能力：当前用户查询、用户私有聊天、用户维度趋势统计。
- 但 mock 的数据模型与后端真实实体尚未完全对齐，尤其是 `ApiKey` 归属用户、聊天 usage 归属用户但 `ApiKey` 为空、聊天附件持久化。

### 2.3 现有后端开发规范

已抽样阅读完整链路：

- Controller: `backend/src/AiRelay.Api/Controllers/ApiKeyController.cs`
- AppService: `backend/src/AiRelay.Application/ApiKeys/AppServices/ApiKeyAppService.cs`
- DomainService: `backend/src/AiRelay.Domain/ApiKeys/DomainServices/ApiKeyDomainService.cs`
- Repository: `backend/src/AiRelay.Infrastructure/Persistence/Repositories/ApiKeyRepository.cs`
- Repository Interface: `backend/src/AiRelay.Domain/ApiKeys/Repositories/IApiKeyRepository.cs`
- EntityConfiguration: `backend/src/AiRelay.Infrastructure/Persistence/EntityConfigurations/ApiKeyEntityConfiguration.cs`

观察到的规范如下：

- Controller 保持薄层，只做路由、参数绑定、权限边界、返回结果。
- AppService 负责应用层编排、DTO 映射、调用领域服务与仓储。
- DomainService 承担业务规则校验与领域决策。
- Repository 负责查询拼装、分页、包含导航属性的详情加载。
- EF Core `EntityConfiguration` 统一做表结构、索引、字段约束配置。
- 命名上偏向 `XxxAppService`、`XxxDomainService`、`XxxRepository`、`XxxEntityConfiguration`、`InputDto/OutputDto`。
- 接口与实现分层清晰，适合在此基础上扩展“用户域”能力。

---

## 3. 核心问题定义

### 3.1 当前问题

目前 `/workspace` 所需的后端真实能力仍存在缺口：

1. `ApiKey` 缺少归属用户字段，无法天然支持“当前用户名下 API Key”查询。
2. 仪表盘、订阅、使用日志的统计口径与查询口径还没有完全统一到用户维度。
3. 聊天会话与消息目前主要在前端/mock 层成立，后端缺少持久化聚合与 SSE 流式执行入口。
4. 聊天执行虽然和代理转发本质相同，都会经历“选号、构造上游请求、流式消费、记录 usage”，但现有能力主要封装在 `SmartReverseProxyMiddleware.InvokeAsync` 中，复用边界不够好。
5. 聊天消息产生 usage 后，需要落到使用记录表中，但该记录不对应某个用户 API Key，列表需显示空 API Key。

### 3.2 总体设计原则

整体采用两条主线：

- 数据模型主线：给现有“订阅、使用记录、ApiKey”补足 `UserId` 归属，让 `/platform` 与 `/workspace` 共享同一套数据源，只在查询维度上不同。
- 执行链路主线：把 `SmartReverseProxyMiddleware` 中可复用的“模型调用编排”提炼为应用/领域服务，供代理请求、模型测试、工作区聊天统一调用。

---

## 4. 领域模型调整方案

### 4.1 ApiKey

#### 调整内容

为 `ApiKey` 增加用户归属字段。

建议：

- 新增 `UserId`，类型与现有用户主键保持一致。
- 创建 `ApiKey` 时不由前端传入，后端从 `ICurrentUser` 注入。
- 增加索引：
  - `(UserId, IsDeleted)` 或等价查询索引
  - 如常用按名称搜索，可考虑 `(UserId, Name)`

#### 价值

- 订阅管理、仪表盘统计、工作区筛选均可直接按当前用户名下 `ApiKey` 查询。
- `/platform` 管理端后续也可支持查看某用户名下所有 key。

### 4.2 UsageRecord

#### 调整内容

建议为 `UsageRecord` 补充用户归属，并允许聊天类记录 `ApiKeyId/ApiKeyName` 为空。

建议：

- 新增 `UserId`
- `ApiKeyId` 改为可空或允许特定来源为空
- `ApiKeyName` 改为可空
- 新增来源枚举或区分字段，例如：
  - `UsageSource = ApiProxy | WorkspaceChat | ModelTest`

#### 价值

- 使用日志与趋势图可以统一使用一张 usage 表，不需要给聊天单独建一套统计表。
- 聊天记录显示 API KEY 为空的需求可以自然表达。
- 后续若要区分“工作区聊天”和“模型测试”的 usage 来源也更容易。

### 4.3 ChatSession / ChatMessage / ChatAttachment

#### 建议聚合

新增用户私有聊天聚合：

- `ChatSession`
- `ChatMessage`
- `ChatAttachment` 或消息内嵌附件值对象

建议字段：

`ChatSession`

- `Id`
- `UserId`
- `Title`
- `ProviderGroupId`
- `AccountId` 可空，空表示自动调度
- `ModelId`
- `LastMessageTime`

`ChatMessage`

- `Id`
- `ChatSessionId`
- `Role`
- `Content`
- `Status`，可选，若要支持失败态可补
- `CreationTime`

`ChatAttachment`

- `Id`
- `ChatMessageId`
- `MimeType`
- `FileName`
- `StorageKey` 或 `Data`/`Url`
- `Size`

#### 设计要点

- 用户只能访问自己的会话。
- 会话标题可采用“首条用户消息摘要自动命名 + 手工修改”的简单策略。
- 附件若后续要支持对象存储，建议实体中存 `StorageKey`/`Url`，不要长期直接存 base64 大字段。
- `AccountId` 保持可空即可，走“可选直连，默认自动调度”的简单路径。

---

## 5. 接口复用与查询口径方案

### 5.1 `/platform` 与 `/workspace` 的复用原则

建议优先复用以下能力：

- 订阅列表接口：同一套 `ApiKey` 查询模型
- 使用记录列表接口：同一套 `UsageRecord` 查询模型
- 使用记录统计接口：同一套 metric 聚合
- 账号选择与上游调用：同一套模型执行编排

实现方式不是“前端调同一路由但写死过滤条件”，而是后端将“当前用户上下文”纳入查询条件。

### 5.2 推荐的接口组织方式

可以保留现有平台接口，同时新增或扩展工作区友好接口：

- `/api/v1/api-keys`
- `/api/v1/usage-records`
- `/api/v1/usage-record-metrics`
- `/api/v1/chat-sessions`

然后在 AppService 内根据调用场景做两类入口：

- 管理视角：允许更多筛选条件
- 当前用户视角：默认注入 `CurrentUserId`

更推荐的做法是：

- 在查询 DTO 中新增 `OnlyCurrentUser` 或内部参数 `userId`
- Controller 不暴露用户 ID 给前端
- AppService 读取 `ICurrentUser` 后拼装查询条件

这样可以最大限度复用仓储查询逻辑。

### 5.3 仪表盘统计口径

`/workspace` 仪表盘应明确按“当前用户名下 API Key + 当前用户聊天 usage”统计。

建议分为两层：

- 订阅相关指标：仅统计 `ApiKey.UserId == currentUserId`
- usage 趋势相关指标：统计 `UsageRecord.UserId == currentUserId`

这样能同时覆盖：

- API Key 调用趋势
- 费用、token、成功率、平均耗时
- 最近活动
- 聊天 usage 与代理 usage 统一纳入用户视图

---

## 6. 聊天执行链路复用方案

### 6.1 现状判断

已阅读：

- `backend/src/AiRelay.Api/Middleware/SmartProxy/SmartReverseProxyMiddleware.cs`
- `frontend/src/app/features/platform/components/account-token/widgets/model-test-dialog/model-test-dialog.ts`

结论：

- 前端聊天流式协议完全可以沿用模型测试 dialog 当前消费模式。
- 后端不应让工作区聊天直接穿过 `SmartReverseProxyMiddleware`。
- 但 `SmartReverseProxyMiddleware` 中的核心编排能力必须被抽出，否则聊天、模型测试、代理三套逻辑会很快分叉。

### 6.2 建议抽取的共享能力

从 `SmartReverseProxyMiddleware` 中抽取一个可复用执行编排服务，名称可为：

- `ChatExecutionAppService`
- 或 `ModelExecutionOrchestrator`

职责建议包括：

1. 根据请求上下文完成选号
2. 处理官方仿真指纹
3. 获取并发槽位
4. 构建具体 handler
5. 调用上游模型
6. 处理流式响应
7. 统一重试、降级、切号
8. 记录 usage
9. 返回标准化流事件

### 6.3 共享服务与调用方关系

建议形成如下结构：

- `SmartReverseProxyMiddleware`
  - 负责 HTTP proxy 入口适配
  - 调用共享执行编排服务
- `Model test AppService / Controller`
  - 负责测试请求 DTO 与 SSE 输出
  - 调用共享执行编排服务
- `WorkspaceChatAppService / Controller`
  - 负责会话读写、消息持久化、SSE 输出
  - 调用共享执行编排服务

这样可以避免把 HTTP middleware 作为“业务服务”反向复用。

### 6.4 推荐的共享输入模型

建议抽象一个输入上下文，例如：

- `ExecuteChatRequestInput`

包含：

- `UserId`
- `ProviderGroupId`
- `AccountId`
- `ModelId`
- `Messages`
- `Attachments`
- `RouteProfile`
- `Source`
- `CorrelationId`

输出则统一为：

- 流式事件枚举
- 最终 usage
- 实际使用的账号与模型
- 错误信息

### 6.5 待确认决策点

当前仍有一个关键问题需要明确：

`workspace chat` 中 `providerGroupId + modelId` 如何确定 `RouteProfile`？

可选方向：

1. 前端显式传 `RouteProfile`
2. 后端根据 `ProviderGroup`、模型能力、账号协议自动推导
3. 聊天仅允许落到某一类固定 profile

从现有代码与复用目标看，更建议后端推导，但需要明确规则来源。  
在该规则未定前，共享执行编排服务的入参结构和选号逻辑还不能完全落地。

---

## 7. 分层实施方案

### 7.1 Domain 层

新增/调整：

- `ApiKeys/Entities/ApiKey` 增加 `UserId`
- `UsageRecords/Entities/UsageRecord` 增加 `UserId`、来源字段、可空 `ApiKeyId/ApiKeyName`
- 新增 `ChatSessions/Entities/ChatSession`
- 新增 `ChatSessions/Entities/ChatMessage`
- 新增 `ChatSessions/Entities/ChatAttachment` 或附件值对象
- 新增 `ChatSessions/Repositories/IChatSessionRepository`
- 新增 `ChatSessions/DomainServices/ChatSessionDomainService`
- 视情况新增共享执行领域服务接口

领域规则建议放置：

- 只能修改当前用户自己的会话
- 删除会话时级联处理消息与附件
- 会话标题生成规则
- 消息发送前的参数校验
- 聊天消息附件数量、大小、类型校验

### 7.2 Application 层

新增/调整：

- `ApiKeyAppService`
  - 创建 `ApiKey` 时注入当前用户
  - 查询时支持 current user 口径
- `UsageRecordAppService` / `UsageRecordMetricAppService`
  - 增加按当前用户维度查询与聚合
- 新增 `ChatSessionAppService`
  - 查询会话列表
  - 查询会话详情
  - 创建会话
  - 更新会话配置
  - 删除会话
  - 发送消息并输出 SSE
- 新增共享执行编排应用服务

DTO 建议：

- `CreateChatSessionInputDto`
- `UpdateChatSessionInputDto`
- `SendChatMessageInputDto`
- `ChatSessionOutputDto`
- `ChatMessageOutputDto`
- `ChatAttachmentOutputDto`
- `WorkspaceDashboardMetricOutputDto` 或继续复用现有 metric DTO

### 7.3 Infrastructure 层

新增/调整：

- `ApiKeyRepository` 增加按用户过滤查询方法
- `UsageRecordRepository` 增加按用户分页和聚合
- 新增 `ChatSessionRepository`
- 新增相关 `EntityConfiguration`
- 新增数据库迁移
- 若附件落对象存储，则增加存储服务实现

仓储接口建议尽量显式体现查询目的，例如：

- `GetPagedListByUserAsync`
- `GetMetricByUserAsync`
- `GetWithDetailsByUserAsync`

避免把过多用户隔离逻辑散落在 Controller 或前端。

### 7.4 API 层

新增/调整：

- `ChatSessionController`
- 现有 `ApiKey`、`UsageRecord`、`UsageRecordMetric` Controller 根据当前用户提供工作区友好查询
- SSE 端点返回结构对齐现有模型测试 dialog 事件格式

Controller 只负责：

- 取 `ICurrentUser`
- 传入 AppService
- 设置 SSE Header
- 输出事件流

---

## 8. 前端与 Mock 配套改造建议

虽然本次文档重点在后端，但前端与 mock 需要提前对齐真实模型。

### 8.1 `_mock/data` 需要向真实实体对齐

建议统一补齐：

- `ApiKey` mock 数据增加 `userId`
- `UsageRecord` mock 数据增加 `userId`、`source`、可空 `apiKeyId/apiKeyName`
- `ChatSession` mock 数据保证 `userId`
- `ChatMessage` mock 数据补充附件结构

### 8.2 `_mock/api` 需要体现真实后端隔离方式

建议：

- 所有 workspace 查询都经 `current-user` 工具获取当前用户
- 不允许前端传 `userId` 来筛选
- 聊天发送消息时在 mock 中同步写入：
  - 用户消息
  - assistant 流式回复
  - usage record

### 8.3 前端 service 需要保持的抽象

建议保持现状方向：

- Dashboard、订阅、使用日志沿用现有 service
- 聊天沿用当前 SSE 消费模式
- 不在前端拼接“只看当前用户”的显式参数，等待后端内部隔离

---

## 9. 数据库与迁移计划

### 9.1 结构变更

建议至少包含以下迁移：

1. `ApiKeys` 表增加 `UserId`
2. `UsageRecords` 表增加 `UserId`
3. `UsageRecords` 表允许 `ApiKeyId/ApiKeyName` 为空
4. `UsageRecords` 表增加 `Source`
5. 新增 `ChatSessions`
6. 新增 `ChatMessages`
7. 新增 `ChatAttachments`

### 9.2 索引建议

- `ApiKeys(UserId, CreationTime desc)`
- `UsageRecords(UserId, CreationTime desc)`
- `UsageRecords(UserId, Source, CreationTime desc)`
- `ChatSessions(UserId, LastMessageTime desc)`
- `ChatMessages(ChatSessionId, CreationTime asc)`

### 9.3 数据回填

若历史 `ApiKey` 与用户已有间接关联，需要补一轮数据回填脚本。  
若历史数据无法准确映射用户，则需要在迁移方案中明确：

- 老数据是否仅供管理端查看
- workspace 是否只展示迁移后新数据

这是上线前必须确认的风险项。

---

## 10. 实施阶段建议

### Phase A：统一用户归属基础数据

目标：

- `ApiKey`、`UsageRecord` 都具备 `UserId`
- 订阅和使用日志查询可按当前用户隔离

任务：

1. `ApiKey` 增加 `UserId`
2. `UsageRecord` 增加 `UserId` 与来源字段
3. 改造相关 repository 与 app service
4. 调整 mock 数据结构
5. 打通 `/workspace/my-subscriptions`
6. 打通 `/workspace/usage-logs`
7. 打通 `/workspace/dashboard` 的用户维度统计

### Phase B：抽取共享模型执行编排

目标：

- 把 `SmartReverseProxyMiddleware` 中业务可复用部分下沉为共享服务

任务：

1. 抽取选号、并发、指纹、handler 创建、重试、usage 记录逻辑
2. 保证 middleware 仍然通过共享服务工作
3. 让模型测试接口也接入共享服务
4. 明确 `RouteProfile` 推导策略

### Phase C：聊天持久化与 SSE 接入

目标：

- `/workspace/chat` 全量接真实后端

任务：

1. 建 `ChatSession` 聚合
2. 建聊天 controller/app service/repository
3. 接入共享执行编排服务
4. 落消息、附件、usage
5. SSE 输出对齐前端既有消费方式
6. mock 切换为真实接口

### Phase D：联调与验收

目标：

- `/workspace` 四个页面全部落到真实后端

任务：

1. 前后端联调
2. 验证数据隔离
3. 验证聊天 usage 与 API usage 统计口径
4. 验证空 `API KEY` 展示
5. 验证异常重试、切号、流中断行为

---

## 11. 验收标准

### 11.1 订阅与日志

- 普通用户只能看到自己名下 `ApiKey`
- 普通用户只能看到自己的 usage 记录
- `/workspace/dashboard` 的趋势图只统计当前用户数据

### 11.2 聊天

- 用户只能访问自己的聊天会话
- 新建、删除、发送消息后刷新页面仍保留
- 附件可持久化并回显
- 流式输出格式与当前前端兼容
- 聊天 usage 成功落库
- 聊天 usage 在使用日志中 `API KEY` 为空

### 11.3 复用与代码质量

- 代理请求、模型测试、workspace chat 不复制三套选号重试逻辑
- Controller 维持薄层
- 查询隔离不放在前端
- 新增仓储与 DTO 命名、分层风格与现有 `ApiKey` 链路一致

---

## 12. 风险与待确认项

### 12.1 必须确认

1. `providerGroupId + modelId` 到 `RouteProfile` 的推导规则
2. 旧 `ApiKey` 数据如何回填 `UserId`
3. 聊天附件最终存数据库、文件系统还是对象存储
4. 聊天 usage 是否计入与代理 usage 完全相同的费用口径

### 12.2 实施风险

- 若不先抽共享执行编排，聊天与代理会快速分叉。
- 若 `UsageRecord` 不补 `UserId`，工作区统计仍会依赖脆弱的 join/间接过滤。
- 若 `ApiKeyId` 不允许为空，聊天 usage 需要额外伪造订阅，模型会变脏。

---

## 13. 建议的代码落点清单

建议优先在以下目录落代码：

- `backend/src/AiRelay.Domain/ApiKeys/...`
- `backend/src/AiRelay.Domain/UsageRecords/...`
- `backend/src/AiRelay.Domain/ChatSessions/...`
- `backend/src/AiRelay.Application/ChatSessions/...`
- `backend/src/AiRelay.Application/UsageRecords/...`
- `backend/src/AiRelay.Infrastructure/Persistence/Repositories/...`
- `backend/src/AiRelay.Infrastructure/Persistence/EntityConfigurations/...`
- `backend/src/AiRelay.Api/Controllers/...`
- `backend/src/AiRelay.Api/Middleware/SmartProxy/...`

---

## 14. 结论

这次改造的正确方向不是“为 `/workspace` 补几个独立接口”，而是把现有平台能力提升到“天然支持当前用户隔离”和“聊天/代理/模型测试共享同一套模型执行编排”。

最先应落地的是两项基础改造：

1. 给 `ApiKey`、`UsageRecord` 补 `UserId`，统一用户维度查询与统计。
2. 从 `SmartReverseProxyMiddleware` 抽取共享执行编排服务。

在这两项完成后，聊天持久化与 `/workspace` 全量真实接入会顺畅很多，也更符合当前仓储、应用服务、领域服务的既有分层规范。
