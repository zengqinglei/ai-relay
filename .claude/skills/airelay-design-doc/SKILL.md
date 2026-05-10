---
name: airelay-design-doc
description: 为 AiRelay 项目需求输出先审查后实施的结构化设计方案
---

# 使用场景

当用户要求“先输出方案”“待我审查”“输出调整内容树形目录结构”“先不要改代码”时使用。

# 固定前置检查

1. 理解用户需求，识别涉及范围：后端、前端、代理链路、文档、参考项目。
2. 如涉及后端，阅读 `docs/standards/code_standard/backend_develop.md`。
3. 如涉及前端，阅读 `docs/standards/code_standard/frontend_develop.md`。
4. 如涉及通用规范，阅读 `docs/standards/code_standard/common_develop.md`。
5. 阅读当前项目中相似模块的代码结构和命名风格。
6. 如涉及 Angular、PrimeNG、TailwindCSS、.NET、第三方 SDK 或类库，查阅对应最新官方文档。
7. 如用户要求对比参考项目，完整阅读相关调用链后再输出结论。

# 输出格式

使用中文输出，结构清晰，标题层次明显：

## 需求理解

说明目标、范围和不做的内容。

## 现状分析

说明当前代码、文档或业务链路的现状，必要时标出关键文件和方法。

## 核心策略

说明推荐方案、关键取舍、为什么这样做。

## 调整内容树形目录结构

用树形结构展示预计新增、修改、删除的文件。

## 核心实现代码

只展示关键接口、DTO、核心方法或组件片段，不输出大量完整代码。

## 实施计划

拆成小步骤，优先保证每一步可验证。

## 风险点与验证方式

说明潜在风险、测试命令、浏览器验证点或接口验证点。

## 待确认问题

仅列出真正阻塞设计或实现的问题。

# 项目规范

- 后端遵循 Api / Application / Domain / Infrastructure 分层。
- HttpContext 相关逻辑保留在 Api 层。
- Application 负责协调业务逻辑、DTO、Mapping、事件。
- Domain 负责核心业务规则、领域对象行为、领域服务。
- Infrastructure 负责 EF Core 持久化和第三方服务实现。
- 后端使用 .NET 10 / C# 现代语法，命名和 DTO 风格与现有代码一致。
- 前端遵循 Angular v21、PrimeNG v21、TailwindCSS v4 最佳实践。
- 前端优先使用 PrimeNG 默认组件和 design token，减少自定义 CSS。
- 前端变更需同步 service、dto、enum/model、`frontend/_mock/api`、`frontend/_mock/data`。

# 禁止事项

- 用户要求先审查时，不要直接修改代码。
- 不要跳过开发规范文档。
- 不要忽略参考项目差异。
- 不要引入过度设计。
- 不要创建与现有命名风格不一致的目录或类型。
