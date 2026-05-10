---
name: airelay-code-review
description: 审查 AiRelay 当前改动或 git 暂存区代码，结合业务、最佳实践和项目规范输出审查报告
---

# 使用场景

当用户要求审查 git 暂存区、当前改动、某次重构、某个功能实现或 PR 前自查时使用。

典型提示：

```text
审查git暂存的代码：
1、基于代码分析调整的业务
2、基于业务审视当前的实现是否存在bug、代码是否足够精简
3、基于引入的组件、类库查阅相关的最新版本官方文档，并基于最佳实践审视当前的实现是否符合
4、是否符合.net 10、angular v21的最佳开发规范风格
```

# 固定前置检查

1. 查看 git 状态、暂存区 diff、未暂存 diff。
2. 先根据代码反推出本次调整的业务目标和影响范围。
3. 阅读相关业务的现有代码，不能只看 diff。
4. 如涉及后端，阅读 `docs/standards/code_standard/backend_develop.md`。
5. 如涉及前端，阅读 `docs/standards/code_standard/frontend_develop.md`。
6. 如涉及通用规范，阅读 `docs/standards/code_standard/common_develop.md`。
7. 如引入或修改组件、类库、SDK、框架能力，查阅相关最新版本官方文档，并依据最佳实践审查实现。
8. 如涉及 Angular 项目，先调用 Angular MCP `list_projects`，修改或判断 Angular 代码前调用 `get_best_practices`。
9. 如涉及代理转发链路，审查代理入口与模型测试入口完整链路。

# 审查维度

## 1. 业务理解

- 本次代码实际调整了什么业务。
- 是否与用户需求或方案一致。
- 是否有遗漏的业务分支。
- 是否破坏历史行为。

## 2. Bug 与逻辑严谨性

- 是否存在空值、枚举、分页、排序、并发、事务、权限、认证、状态流转问题。
- 前后端接口契约是否一致。
- DTO、Entity、Mapping、Mock 是否同步。
- 错误处理是否能让调用方正确感知。
- 日志和使用记录是否包含关键字段。

## 3. 精简性与设计质量

- 代码是否足够精简。
- 是否存在过度设计、过度抽象、过度拆分。
- 是否存在重复逻辑、死代码、未清理代码。
- 职责是否单一。
- 命名是否与项目现有风格一致。

## 4. 后端规范

- 是否符合 .NET 10 / C# 现代开发风格。
- 是否合理使用主构造函数、record、集合表达式、模式匹配等现代语法。
- 是否符合 Api / Application / Domain / Infrastructure 分层。
- HttpContext 相关逻辑是否只在 Api 层。
- Application 是否只做协调。
- Domain 是否承载核心业务规则。
- Infrastructure 是否只做持久化和外部服务实现。
- DTO 命名、分页 DTO、AutoMapper、枚举序列化是否符合规范。
- 异步方法、CancellationToken、日志、异常是否符合项目规范。

## 5. 前端规范

- 是否符合 Angular v21 最佳实践。
- 是否优先使用 PrimeNG v21 官方组件。
- 是否符合 TailwindCSS v4 使用方式。
- 是否保持 PrimeNG 默认风格和 design token。
- 是否支持亮暗主题自动适配。
- 页面、widgets、service、dto、enum/model、mock 的目录和命名是否一致。
- 是否存在冗余 CSS、冗余状态、冗余模板逻辑。

## 6. 官方文档与最佳实践

- 对新增或关键使用的组件、类库、SDK、框架 API 查阅最新官方文档。
- 对照官方推荐方式判断当前实现是否合理。
- 如果当前实现与官方推荐不同，说明差异、风险和是否需要调整。

## 7. 代理链路专项

如涉及代理、模型测试、ChatModelHandler、RequestProcessor、ResponseProcessor、UsageRecord，需要额外审查：

- 代理入口 `SmartReverseProxyMiddleware.InvokeAsync` 完整链路。
- 模型测试入口 `AccountTokenAppService.DebugModelAsync` 完整链路。
- Claude、Gemini、OpenAI、Antigravity 是否都覆盖。
- OAuth / API Key / Account 分支是否都覆盖。
- Url、Query、Header、RequestBody、ResponseHeader、ResponseBody 处理是否一致。
- SSE / 非流式、同步 / 异步是否正确。
- Body 是否存在多次读取。
- Token、费用、错误、耗时、上下游日志是否记录完整。

# 输出格式

使用中文，优先输出可执行的审查结论：

## 审查结论

- 是否建议通过。
- 是否存在阻塞问题。

## 本次调整的业务理解

基于代码说明本次实际改了什么。

## 阻塞问题

列出必须修复的问题，包含文件路径、问题原因、建议修复方式。

## 非阻塞问题

列出建议修复但不阻塞的问题。

## 精简性与设计建议

指出冗余代码、过度设计、不合理拆分或可删除内容。

## 官方文档与最佳实践核对

说明查阅了哪些官方文档或最佳实践点，当前实现是否符合。

## 规范符合性

分别说明 .NET 10、Angular v21、PrimeNG v21、TailwindCSS v4、项目分层规范的符合情况。

## 建议修复清单

按优先级列出下一步建议。

# 禁止事项

- 不要只复述 diff。
- 不要在未理解业务的情况下给结论。
- 不要跳过官方文档核对。
- 不要把风格问题和真实 bug 混为一谈。
- 不要默认修改代码，除非用户明确要求修复。
