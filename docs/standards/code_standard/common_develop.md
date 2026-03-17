# 项目通用开发规范

## 1、代码提交流程（Git 提交工作流）

- **分支模型**: 采用 `GitFlow` 的变种。
  - `main`: 对应生产环境的稳定版本，受保护，只能通过 Pull Request 合并。
  - `develop`: 开发主分支，集成所有已完成的功能。
  - `feature/*`: 开发新功能的分支，从 `develop` 创建，完成后合并回 `develop`。
  - `hotfix/*`: 修复 `main` 分支的紧急 Bug，从 `main` 创建，完成后同时合并到 `main` 和 `develop`。

## 2. Git 提交规范

为了保证 Git 历史的清晰和可追溯性，所有提交必须遵循 **[Conventional Commits](https://www.conventionalcommits.org/)** 规范。

- **格式**: `<type>(<scope>): <subject>`
  - `type` 和 `scope` **必须**为小写英文。
  - `subject` **必须**为中文，清晰描述本次提交的内容。
- **类型 (type)**:
  - `feat`: 新功能
  - `fix`: Bug修复
  - `docs`: 文档变更
  - `style`: 代码格式（不影响代码运行的变动）
  - `refactor`: 重构（既不是新增功能，也不是修改bug的代码变动）
  - `test`: 增加测试
  - `chore`: 构建过程或辅助工具的变动
- **示例**:
  - `git commit -m "feat(auth): 新增用户登录页面"`
  - `git commit -m "fix(order): 修正订单总价的计算错误"`

## 3. 通用开发原则

- **依赖最小化**: 仅在没有合理替代方案时才引入新的第三方依赖。优先使用框架或平台已提供的内置功能。
- **代码质量**: 遵循 **DRY** (Don't Repeat Yourself) 原则，使用有意义的命名，并为复杂逻辑添加必要的注释。
- **文档同步**: 任何对代码库的重大更改（如添加新 API、修改架构）都必须同步更新相关文档。