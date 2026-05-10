---
name: airelay-fullstack-feature
description: 为 AiRelay 项目前后端联动需求制定方案并实施
---

# 使用场景

当前后端 `backend/src` 与前端 `frontend` 都需要调整，或用户明确要求接口、DTO、页面、`_mock` 同步调整时使用。

# 固定前置检查

1. 阅读 `docs/standards/code_standard/backend_develop.md`。
2. 阅读 `docs/standards/code_standard/frontend_develop.md`。
3. 如涉及通用规范，阅读 `docs/standards/code_standard/common_develop.md`。
4. 阅读后端相似业务模块，确认 Entity、DomainService、AppService、Controller、DTO、Mapping 的命名和目录风格。
5. 调用 Angular MCP `list_projects` 识别 Angular workspace；修改 Angular 代码前调用 `get_best_practices`。
6. 阅读前端相似页面，确认 page、widgets、service、dto、enum/model、mock 的组织方式。
7. 使用 PrimeNG 组件前查阅 PrimeNG v21 官方文档；涉及 TailwindCSS 或 Angular 新语法时查阅对应官方文档。

# 工作流程

1. 先输出方案，除非用户明确要求直接实现。
2. 明确接口契约：URL、Method、Query、Body、Response、分页结构、枚举 Key/Value。
3. 后端规划：Entity、DomainService、Repository、AppService、Controller、DTO、Mapping、Migration。
4. 前端规划：route、page、widgets、service、dto、enum/model、shared 组件、mock api、mock data。
5. 输出调整内容树形目录结构。
6. 用户确认后再实施。
7. 实施完成后运行必要的构建、类型检查和测试。
8. 前端 UI 变更必须启动项目并用浏览器验证主要路径和边界场景；无法验证时明确说明。

# 后端规范

- HttpContext、Claims、Header、Request/Response 等 HTTP 细节留在 Api 层。
- Application 只协调领域对象、领域服务、仓储、Mapping 和事件。
- Domain 放核心业务规则，领域服务命名为 `XxxDomainService`，默认不声明接口。
- Infrastructure 放 EF Core 仓储和外部服务实现。
- 应用服务继承 `Leistd.Ddd.Application.Contracts.AppService.IAppService`。
- 分页输入继承 `PagedRequestDto` 并以 `XxxPagedInputDto` 结尾。
- 分页输出使用 `PagedResultDto<XxxOutputDto>`。
- DTO 与 Entity 映射优先使用 AutoMapper。
- 枚举数据库存储使用字符串，接口输入输出保持字符串表现。
- 使用 .NET 10 / C# 现代语法，避免冗余空值判断和过度封装。

# 前端规范

- Angular v21 最佳实践。
- PrimeNG v21 官方组件优先，保持默认风格和大小。
- TailwindCSS v4 只做布局和必要样式。
- 主题适配依赖 PrimeNG design token 和 color scheme。
- 子组件放到对应页面 `widgets`。
- 与后端交互必须声明 service 和 dto。
- `_mock/api` 与 `_mock/data` 必须同步真实接口结构。
- 前后端 enum、DTO、字段命名尽量保持一致。

# 输出格式

## 需求理解
## 接口契约
## 后端调整方案
## 前端调整方案
## Mock 调整方案
## 调整内容树形目录结构
## 核心代码实现
## 实施计划
## 验证方式
## 风险点
