/**
 * SSE Mock Registry - 统一管理 SSE 流式请求的 mock 数据
 *
 * 由于原生 fetch() 无法被 Angular HTTP 拦截器拦截，
 * 我们在 NativeFetchService 中实现了 SSE mock 机制。
 *
 * 使用方式：
 * 1. 在各模块的 _mock/api/*.ts 文件中注册 SSE 端点的 mock handler
 * 2. Handler 返回一个数组，每个元素会被格式化为 SSE 事件发送
 * 3. NativeFetchService 会自动处理流式传输和延迟模拟
 */

interface SseMockHandler {
  method: string;
  pattern: RegExp;
  handler: (body?: unknown) => unknown[];
}

class SseMockRegistry {
  private handlers: SseMockHandler[] = [];

  /**
   * 注册 SSE mock handler
   */
  register(method: string, pattern: RegExp, handler: (body?: unknown) => unknown[]): void {
    this.handlers.push({ method, pattern, handler });
  }

  /**
   * 匹配请求并返回对应的 handler
   */
  match(url: string, method: string): ((body?: unknown) => unknown[]) | null {
    const handler = this.handlers.find(h => h.method === method && h.pattern.test(url));
    return handler ? handler.handler : null;
  }
}

export const SSE_MOCK_REGISTRY = new SseMockRegistry();
