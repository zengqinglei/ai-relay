# 指南：Mock 开发

本文档详细介绍了项目中轻量级、高内聚的 Mock 系统。该系统通过 Angular 的 `HttpInterceptor` 在开发环境中拦截 HTTP 请求并返回模拟数据，以支持独立的前端开发和测试。

## 核心理念

- **高内聚**: 所有 Mock 相关的核心逻辑（拦截器、类型、服务提供者）都封装在 `frontend/_mock/core` 目录下。
- **可插拔**: 通过在环境配置文件（如 `environment.dev.ts`）中设置 `useMock`，可以一键启用或禁用整个 Mock 功能。
- **易于扩展**: 添加新的 Mock API 只需在 `_mock/api` 和 `_mock/data` 目录下创建新文件，并在 `_mock/index.ts` 中导出一行即可。

## 如何添加一个新的 Mock API

1.  **定义数据 (可选)**: 如果需要，可以在 `frontend/_mock/data/` 目录下创建 `*.data.ts` 文件来存放纯粹的模拟数据。
    ```typescript
    // in: frontend/_mock/data/product.data.ts
    export const PRODUCTS = [{ id: '1', name: 'Leistd-AI' }];
    ```

2.  **创建 API 处理器**: 在 `frontend/_mock/api/` 目录下创建一个新的 `*.ts` 文件（例如 `product.ts`）。在该文件中，定义并导出一个 `*_API` 对象。对象的键是 API 路径（支持 `METHOD /path` 格式），值是一个处理函数。
    ```typescript
    // in: frontend/_mock/api/product.ts
    import { MockRequest } from '../core/models';
    import { PRODUCTS } from '../data/product.data'; // 从 data 目录导入

    function getProducts(req: MockRequest): any[] {
      // 可根据 req.queryParams 进行过滤、分页等操作
      return PRODUCTS;
    }

    export const PRODUCT_API = {
      'GET /api/v1/products': (req: MockRequest) => getProducts(req),
      'GET /api/v1/products/:id': (req: MockRequest) => PRODUCTS.find(p => p.id === req.params.id)
    };
    ```

3.  **注册 API**: 打开 `frontend/_mock/index.ts` 文件，添加一行以导出您新创建的 API 处理器。
    ```typescript
    // in: frontend/_mock/index.ts
    export * from './api/auth';
    export * from './api/user';
    export * from './api/product'; // 新增此行
    ```

## 高级用法

- **模拟延迟**:
  - **全局延迟**: 在环境配置的 `useMock` 对象中设置 `delay` 属性（单位：毫秒）。
  - **单个 API 延迟**: 在 Mock 处理函数中返回一个 `MockResponse` 对象，并设置其 `delay` 属性。

- **排除特定 API**: 在 `useMock` 配置中，通过 `exclude` 属性指定一个或多个 API 路径（支持字符串或正则表达式），这些 API 将不会被 Mock，而是直接请求真实后端。

- **自定义响应**: Mock 处理函数可以返回一个 `MockResponse` 对象，以自定义 `status`、`headers` 和 `body`。

- **异步操作**: Mock 处理函数可以是 `async` 函数，允许执行如文件读取等异步操作。

## 配置文件示例 (`environment.dev.ts`)

```typescript
export const environment: Environment = {
  // ...
  useMock: {
    enable: true,
    log: true,
    delay: 300, // 全局延迟300ms
    exclude: [
      '/api/v1/auth/me', // 排除获取当前用户信息的请求
      /^\/api\/v1\/files\// // 使用正则排除所有文件相关的请求
    ]
  },
  // ...
};