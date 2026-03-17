# Mock API 架构说明

## 目录结构

```
_mock/
├── core/                  # Mock 核心机制
│   ├── models.ts         # Mock 类型定义
│   └── sse-mock-registry.ts  # SSE 流式请求注册表
├── api/                   # API 拦截规则
│   ├── index.ts          # HTTP 拦截器注册（HttpClient 请求）
│   └── *.ts              # 各模块的 HTTP/SSE mock 规则
└── data/                 # Mock 数据源（单一数据源）
    └── *.ts              # 各模块的 mock 数据
```

## 两种 Mock 机制

### 1. HTTP Mock（通过 Angular 拦截器）

**适用场景：** 所有通过 `HttpClient` 发起的请求（GET/POST/PUT/DELETE 等）

**工作原理：**
- `_mock/api/index.ts` 注册拦截规则
- Angular 的 `MockInterceptor` 自动拦截匹配的请求
- 返回 `_mock/data/` 中的数据

**示例：**
```typescript
// _mock/api/account-token.ts
export const ACCOUNT_TOKEN_API = {
  'GET /api/v1/account-tokens': () => ({
    items: MOCK_ACCOUNT_TOKENS,
    total: MOCK_ACCOUNT_TOKENS.length
  })
};
```

---

### 2. SSE Mock（通过 NativeFetchService）

**适用场景：** 所有 SSE 流式请求（使用原生 `fetch()` 的场景）

**为什么需要单独处理：**
- 原生 `fetch()` **不会被 Angular HTTP 拦截器拦截**
- SSE 需要 `ReadableStream` 支持，无法通过普通 HTTP 响应模拟

**工作原理：**
1. `NativeFetchService.fetch()` 在 mock 模式下检查 `SSE_MOCK_REGISTRY`
2. 如果匹配到注册的 SSE 端点，返回伪造的 `Response` 对象
3. `Response.body` 是一个 `ReadableStream`，模拟真实 SSE 流
4. 数据来源同样是 `_mock/data/`（单一数据源）

**示例：**
```typescript
// _mock/api/account-token.ts
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { MOCK_CHAT_STREAM_CHUNKS } from '../data/account-token';

SSE_MOCK_REGISTRY.register(
  'POST',
  /\/api\/v1\/account-tokens\/[^/]+\/model-test$/,
  (body?: unknown) => {
    console.log('[SSE Mock] Model test:', body);
    return MOCK_CHAT_STREAM_CHUNKS;  // 返回数组，每个元素会被格式化为 SSE 事件
  }
);
```

---

## 添加新的 SSE Mock

### 步骤 1：准备 Mock 数据

在 `_mock/data/` 中定义数据：

```typescript
// _mock/data/your-module.ts
export const MOCK_SSE_EVENTS = [
  { content: 'Hello ' },
  { content: 'World!' },
  { isComplete: true }
];
```

### 步骤 2：注册 SSE Mock

在对应模块的 `_mock/api/*.ts` 文件中注册：

```typescript
// _mock/api/your-module.ts
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { MOCK_SSE_EVENTS } from '../data/your-module';

SSE_MOCK_REGISTRY.register(
  'POST',                              // HTTP 方法
  /\/api\/v1\/your-endpoint$/,         // URL 正则匹配
  (body?: unknown) => {                // Handler 函数
    // 可以根据 body 返回不同的数据
    return MOCK_SSE_EVENTS;
  }
);
```

### 步骤 3：Service 层正常调用

Service 层无需任何特殊处理，直接使用 `NativeFetchService`：

```typescript
export class YourService {
  private readonly nativeFetchService = inject(NativeFetchService);

  streamData(): Observable<Event> {
    return new Observable(observer => {
      this.nativeFetchService
        .fetch('/api/v1/your-endpoint', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ param: 'value' })
        })
        .then(async response => {
          const reader = response.body.getReader();
          // ... 标准 SSE 流处理逻辑
        });
    });
  }
}
```

---

## 统一开发体验

✅ **HTTP 请求：** 在 `_mock/api/index.ts` 注册
✅ **SSE 流式请求：** 在各模块的 `_mock/api/*.ts` 中通过 `SSE_MOCK_REGISTRY` 注册
✅ **数据源：** 统一在 `_mock/data/` 管理
✅ **Service 层：** 无需判断 mock 模式，透明切换

Mock 模式通过 `environment.useMock.enable` 控制，真实/mock 环境无缝切换。
