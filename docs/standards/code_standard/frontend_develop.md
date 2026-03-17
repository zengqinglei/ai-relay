# 前端开发规范

本文档为基于 Angular 21、PrimeNG 21、Tailwind CSS 4 技术栈的前端项目开发规范。

> **注意**: 本文档中的所有开发活动，都必须同时遵循 **[项目通用开发规范](./common_develop.md)** 中定义的 Git 工作流和提交规范。

---

## 目录

1. [核心技术栈](#1-核心技术栈)
2. [目录结构](#2-目录结构)
3. [编码规范](#3-编码规范)
4. [组件与样式规范](#4-组件与样式规范)
5. [开发原则](#5-开发原则)
6. [共享资源](#6-共享资源)

---

## 1. 核心技术栈

- **前端框架**: Angular v21+
- **UI 组件库**: PrimeNG v21+
- **原子化CSS**: Tailwind CSS v4+
- **命令行工具**: Angular CLI v21+
- **开发语言**: TypeScript 5.8+
- **状态管理**: Angular Signals

---

## 2. 目录结构

遵循关注点分离原则，组织清晰的目录结构。

```
frontend/
├── _mock/                                    # Mock 服务 (完全独立于源码，用于前端独立开发和测试)
│   ├── api/                                  # API Mock 处理器 (模拟后端API端点)
│   │   └── *.ts
│   ├── data/                                 # 纯粹的模拟数据源
│   │   └── *.ts
│   └── index.ts                              # Mock 服务启动入口（用于导出api）
├── src/
│   ├── app/
│   │   ├── core/                             # 核心逻辑 (非UI, 应用级单例服务和配置)
│   │   │   ├── guards/                       # 路由守卫
│   │   │   │   └── auth-guard.ts             #   - e.g., 检查用户是否登录
│   │   │   ├── interceptors/                 # HTTP 拦截器
│   │   │   │   └── token-interceptor.ts      #   - e.g., 自动为请求附加认证Token
│   │   │   ├── handlers/                     # 处理程序
│   │   │   │   └── global-error-handler.ts   #   - e.g., 全局异常处理
│   │   │   └── services/                     # 应用级核心服务 (非业务，提供基础能力)
│   │   │       └── startup-service.ts        #   - 应用初始化服务 (用于 APP_INITIALIZER)
│   │   ├── features/                         # 业务功能模块 (按业务领域划分)
│   │   │   └── {module-name}/                # 单个业务模块 (e.g., products, users)
│   │   │       ├── components/               # 页面级"智能"组件 (Smart Components)
│   │   │       │   └── {page-name}/          #   - 负责业务逻辑、状态管理和与服务交互
│   │   │       │       ├── {page-name}.html
│   │   │       │       ├── {page-name}.css
│   │   │       │       └── {page-name}.ts    # v20+ 规范: 移除 .component 后缀
│   │   │       ├── widgets/                  # 特性内可复用的"哑"组件 (Dumb Components)
│   │   │       │   └── {widget-name}/        #   - 只在此特性内部复用，不具备全局性
│   │   │       │       ├── {widget-name}.html
│   │   │       │       ├── {widget-name}.css
│   │   │       │       └── {widget-name}.ts
│   │   │       ├── resolvers/                # 路由数据解析器 (在路由激活前预先获取数据)
│   │   │       │   └── {feature}-resolver.ts
│   │   │       ├── services/                 # 业务服务 (实现该特性的业务逻辑和API调用)
│   │   │       │   └── {feature}-service.ts
│   │   │       ├── models/                   # 数据模型 (定义该特性的数据结构)
│   │   │       │   ├── {feature}.dto.ts      #   - DTO (Data Transfer Object): 精确匹配API契约
│   │   │       │   ├── {feature}.model.ts    #   - 领域模型 (Domain Model): 前端使用的丰富模型，可带方法
│   │   │       │   └── {feature}.enum.ts     #   - 枚举 (Enums): 该特性相关的状态、类型等
│   │   │       └── {module-name}.routes.ts   # 路由定义 (该特性的所有子路由)
│   │   ├── layout/                           # 应用布局 (负责应用的整体视觉结构)
│   │   │   ├── components/                   # 布局共享组件 (e.g., header, footer, sidebar)
│   │   │   ├── default/                      # 默认布局 (用于大部分需要登录的后台页面)
│   │   │   ├── empty/                        # 空白布局 (用于登录、404、打印等无导航的页面)
│   │   │   ├── landing/                      # 落地页布局 (用于无需登录的营销或产品介绍页)
│   │   │   └── services/                     # 布局服务 (管理布局状态和主题)
│   │   │       ├── layout-service.ts         #   - 管理侧边栏开关、面包屑等状态
│   │   │       └── theme-service.ts          #   - 管理应用主题 (e.g., light/dark mode)
│   │   ├── shared/                           # 全局共享资源 (跨所有业务特性复用，必须是纯粹、通用的)
│   │   │   ├── components/                   # 全局可复用的"哑"组件 (UI-Kit, 只负责展示和交互，不含业务逻辑)
│   │   │   │   ├── button/                   #   - 自定义按钮
│   │   │   │   ├── card/                     #   - 通用卡片容器
│   │   │   │   └── modal/                    #   - 模态框/对话框框架
│   │   │   ├── directives/                   # 全局可复用的属性/结构指令
│   │   │   │   └── highlight.ts              #   - v20+ 规范: 移除 .directive 后缀
│   │   │   ├── pipes/                        # 全局可复用的管道
│   │   │   │   └── format-date-pipe.ts       #   - v20+ 规范: {pipe-name}-pipe.ts
│   │   │   └── models/                       # 全局共享的数据模型 (仅当模型被多个不相关特性使用时)
│   │   │       ├── user.model.ts             #   - e.g., User模型几乎在所有地方都可能用到
│   │   │       └── product.model.ts          #   - e.g., Product模型可能在订单、购物车、推荐等多个特性中用到
│   │   ├── app.config.ts                     # 应用级配置 (依赖注入、提供商、拦截器注册)
│   │   ├── app.ts                            # 应用根组件 (v20+ 规范)
│   │   ├── app.routes.ts                     # 应用主路由 (定义布局与特性模块的懒加载关系)
│   │   └── main.ts                           # 应用启动文件 (bootstrapApplication)
│   ├── assets/                               # 静态资源 (图片, 字体, i18n文件等)
│   │   ├── i18n/                             # 国际化语言文件
│   │   └── icons/                            # SVG 图标
│   └── environments/                         # 环境配置 (用于区分不同部署环境的变量)
│       ├── environment.base.ts               # 基础环境配置 (所有环境共享的通用变量)
│       ├── environment.ts                    # 默认开发环境 (ng serve 时使用)
│       ├── environment.prod.ts               # 生产环境 (ng build --configuration production 时使用)
│       ├── environment.test.ts               # 测试环境 (e.g., 用于QA服务器或自动化测试)
│       └── environment.debug.ts              # 本地调试环境 (用于需要开启特殊调试标志的本地开发)
└── ... (package.json, angular.json, etc.)
```

---

## 3. 编码规范

### 3.1 编码风格

- **必须** 遵循 **[Angular 官方代码风格指南](https://angular.io/guide/styleguide)**
- 使用 ESLint 和 Prettier 进行静态检查与自动格式化

### 3.2 命名约定

遵循 Angular v21+ 的简化风格：

| 类型 | 命名规范 | 示例 |
|------|---------|------|
| Component | `{name}.ts` | `user-profile.ts` |
| Service | `{name}-service.ts` | `user-service.ts` |
| Directive | `{name}.ts` | `highlight.ts` |
| Pipe | `{name}-pipe.ts` | `format-date-pipe.ts` |
| Guard | `{name}-guard.ts` | `auth-guard.ts` |

**文件命名风格**:
- Component: `xxx.ts`（参考现有组件命名风格）
- Service: `xxx-service.ts`（参考���有服务命名风格）

### 3.3 类型驱动

- 所有 API 的请求参数和响应数据 **必须** 使用 `interface` 或 `class` 进行严格定义
- JSDoc 应用于描述方法的功能和业务逻辑，**严禁** 在 JSDoc 中重复 TypeScript 的类型定义

---

## 4. 组件与样式规范

### 4.1 组件库优先

- **必须** 首先在 [PrimeNG 官方文档](https://primeng.org/) 中寻找现成组件
- 尽量使用 PrimeNG v21 组件以及默认风格
- 仅在无法满足需求时才可创建自定义组件

### 4.2 样式方案

- **必须** 优先使用 Tailwind CSS v4 的原子类进行布局和微调
- 自定义样式使用 Tailwind CSS v4
- 任何自定义样式都**必须**与 PrimeNG 的主题风格保持一致

### 4.3 组件设计

- **单一职责 (SRP)**: 每个组件应只关注一个功能点
- **数据流**: 遵循"单向数据流"原则（`[input]` 向下, `(output)` 向上）
- **变更检测**: 为提高性能，所有共享/展示型组件都应使用 `changeDetection: ChangeDetectionStrategy.OnPush`

---

## 5. 开发原则

### 5.1 依赖注入

- **必须** 使用 `inject()` 函数进行依赖注入
- 构造函数 (`constructor`) **仅用于** 执行简单的属性赋值

### 5.2 状态管理

- 对于组件内部或简单父子组件间的状态，**必须** 优先使用 Angular 内置的 **Signals**

### 5.3 错误处理

- **必须** 实现一个全局错误处理机制 (`ErrorHandler`)
- 业务代码中**可以**通过 `catchError` 优先处理特定异常，但**严禁**"吞噬"异常

### 5.4 Mock 开发

- 在开发阶段，**应** 为所有后端 API 提供 Mock 实现
- Mock 相关代码**必须**与业务源码分离，并存放在 `_mock` 目录下

### 5.5 测试

- `service`、`pipe` 和包含复杂业务逻辑的函数 **必须** 有单元测试覆盖
- 核心的共享组件和业务流程 **应** 编写组件测试或端到端测试

---

## 6. 共享资源

### 6.1 公共组件位置

前端公共组件在目录 `src/app/shared` 中：

- **分页相关 DTO**: 分页请求和响应���数据模型
- **Logo 组件**: 应用 Logo 组件
- **平台图标**: 各平台的图标组件
- **主题配置**: 主题相关的配置和服务
- **公共常量**: 全局共享的常量定义

### 6.2 使用原则

- 只有被多个不相关特性使用的组件才应放入 `shared`
- 特性内部复用的组件应放在特性的 `widgets` 目录下
- 保持 `shared` 目录的纯粹性和通用性

---

## 附录：规范检查清单

### ✅ 编码规范检查

- [ ] 遵循 Angular v21 最佳风格指南
- [ ] 文件命名符合项目风格
- [ ] 使用 `inject()` 进行依赖注入
- [ ] 所有 API 数据使用类型定义

### ✅ 组件规范检查

- [ ] 优先使用 PrimeNG v21 组件
- [ ] 自定义样式使用 Tailwind CSS v4
- [ ] 组件遵循单一职责原则
- [ ] 展示型组件使用 OnPush 策略

### ✅ 代码质量检查

- [ ] 通过 ESLint 检查
- [ ] 通过 Stylelint 检查
- [ ] 通过 Prettier 格式化
- [ ] 核心服务有单元测试

---

**文档版本**: v3.0
**最后更新**: 2026-03-17
**维护者**: 开发团队
