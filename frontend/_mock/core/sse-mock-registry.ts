/**
 * SSE Mock Registry - 统一管理 SSE 流式请求的 mock 数据
 */

interface SseMockContext {
  method: string;
  url: string;
  headers?: Headers;
}

interface SseMockHandler {
  method: string;
  pattern: RegExp;
  handler: (body?: unknown, context?: SseMockContext) => unknown[];
}

class SseMockRegistry {
  private handlers: SseMockHandler[] = [];

  register(method: string, pattern: RegExp, handler: (body?: unknown, context?: SseMockContext) => unknown[]): void {
    this.handlers.push({ method, pattern, handler });
  }

  match(url: string, method: string): ((body?: unknown, context?: SseMockContext) => unknown[]) | null {
    const handler = this.handlers.find(h => h.method === method && h.pattern.test(url));
    return handler ? handler.handler : null;
  }
}

export type { SseMockContext };
export const SSE_MOCK_REGISTRY = new SseMockRegistry();
