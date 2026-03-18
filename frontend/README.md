# 前端项目

本项目基于 Angular、PrimeNG 和 Tailwind CSS 构建。

有关详细的开发规范、目录结构和编码准则，请参阅项目根目录下的 [前端项目开发规范](/docs/standards/code_standard/frontend_develop.md) 文件。

---

## 环境准备

在开始之前，请确保您已安装以下必需工具：

- **Node.js**: v18+
- **Angular CLI**: v21+

您可以通过以下命令验证是否已成功安装：
```bash
node --version
ng version
```

---

## 快速开始

### 1. 安装依赖

在首次克隆项目后，请先安装所有必需的依赖项：

```bash
npm install
```

### 2. 复制环境配置文件

复制 `src/environments/environment.dev.ts` 并重命名为 `environment.debug.ts`，内容如下：

```typescript
export const environment = {
  ...environmentBase,
  useMock: {
    enable: false,
    exclude: 'api/v1/users/1',
    delay: 500,
    log: true // 在开发环境默认开启日志
  },
  api: {
    ...environmentBase.api,
    gateway: 'http://localhost:5240'
  }
};
```

### 3. 启动开发服务器

本项目已预置了多套环境配置，您可以根据需要启动对应的开发服务器。

- **启动本地调试环境**（默认使用 `environment.debug.ts` 配置）：
  ```bash
  npm start
  ```

- **启动其他环境**：
  ```bash
  ng serve -c dev     # 开发环境
  ng serve -c test    # 测试环境
  ng serve -c uat     # UAT 环境
  ng serve -c prod    # 生产环境
  ```

服务器启动后，请在浏览器中打开 `http://localhost:4200/`。应用支持热重载，任何对源文件的修改都会自动刷新页面。

---

## 环境配置

项目使用 Angular 的环境配置系统。配置文件位于 `src/environments/`：

- `environment.debug.ts` - 本地调试（`npm start` 默认使用）
- `environment.dev.ts` - 开发环境
- `environment.test.ts` - 测试环境
- `environment.uat.ts` - UAT 环境
- `environment.prod.ts` - 生产环境

### 主要配置选项

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:8080/api',  // 后端 API 地址
  // 在此添加其他环境特定的设置
};
```

使用特定环境：
```bash
ng serve -c <环境名称>
ng build -c <环境名称>
```

---

## 构建项目

您可以根据目标环境构建项目。构建产物将存放在 `dist/` 目录下。

```bash
ng build -c dev         # 开发环境
ng build -c test        # 测试环境
ng build -c uat         # UAT 环境
ng build -c production  # 生产环境（已优化性能）
```

### Docker 构建

项目支持两种 Docker 部署模式：

#### 1. 同域部署（默认）

前后端在同一容器内，前端使用相对路径请求后端 API：

```bash
docker build -t ai-relay .
```

#### 2. 前后端分离部署

前端独立部署，需要指定后端 API 地址：

```bash
# 构建时传入后端 API Gateway 地址
docker build --build-arg API_GATEWAY=https://api.example.com -t ai-relay .
```

**说明**：
- `API_GATEWAY` 为后端 API 网关地址
- 构建时会替换 `environment.prod.ts` 中的占位符
- 如果不传入该参数，默认使用空字符串（相对路径）

---

## 代码质量

### 代码检查

运行代码检查：
```bash
npm run lint           # 运行所有检查
npm run lint:ts        # TypeScript/HTML 检查
npm run lint:style     # CSS 检查
npm run format         # 检查代码格式
```

自动修复问题：
```bash
npm run lint:fix       # 修复所有可自动修复的问题
npm run lint:ts:fix    # 修复 TypeScript/HTML 问题
npm run lint:style:fix # 修复 CSS 问题
npm run format:fix     # 自动格式化代码
```

### 预提交钩子

项目使用 Husky 和 lint-staged 在提交前自动运行代码检查：
- TypeScript/HTML 文件使用 ESLint 检查
- CSS 文件使用 Stylelint 检查
- 所有文件使用 Prettier 格式化

---

## 运行测试

- **单元测试**：
  ```bash
  npm test
  ```

- **端到端 (E2E) 测试**：
  ```bash
  ng e2e
  ```
  > **注意**：项目默认未集成 E2E 测试框架，您可根据需要自行添加。

---

## 代码脚手架

使用 Angular CLI 可以快速生成各种类型的文件：

```bash
ng generate component your-component-name
ng generate service your-service-name
ng generate module your-module-name
ng generate --help  # 查看更多可用选项
```

---

## 推荐 VS Code 插件

- **Angular Language Service**: Angular 支持
- **TypeScript Importer**: 自动导入
- **Tailwind CSS IntelliSense**: CSS 智能提示
- **ESLint**: 代码检查支持
- **Prettier**: 代码格式化

---

## 项目结构

```
frontend/
├── src/
│   ├── app/              # 应用组件和模块
│   ├── assets/           # 静态资源（图片、字体等）
│   ├── environments/     # 环境配置
│   ├── index.html        # 主 HTML 文件
│   ├── main.ts           # 应用入口点
│   └── styles.css        # 全局样式
├── .eslintrc.json        # ESLint 配置
├── stylelint.config.mjs  # Stylelint 配置
├── tailwind.config.js    # Tailwind CSS 配置
├── angular.json          # Angular CLI 配置
└── package.json          # 依赖和脚本
```

---

## 更多资源

- **Angular CLI**: [官方文档](https://angular.dev/tools/cli)
- **PrimeNG**: [官方文档](https://primeng.org/)
- **Tailwind CSS**: [官方文档](https://tailwindcss.com/docs)
