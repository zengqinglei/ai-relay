import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

import { SSE_MOCK_REGISTRY } from '../../../../_mock/core/sse-mock-registry';
import { environment } from '../../../environments/environment';

/**
 * NativeFetchService - 封装原生 fetch API，自动处理 gateway 前缀
 * 用于需要使用原生 fetch（如 SSE 流式请求）的场景
 *
 * Mock 模式下会拦截请求并返回模拟的 SSE 流响应
 */
@Injectable({
  providedIn: 'root'
})
export class NativeFetchService {
  private readonly platformId = inject(PLATFORM_ID);

  /**
   * 构建完整的 URL（自动添加 gateway）
   */
  private buildFullUrl(url: string): string {
    // 如果已经是完整 URL，直接返回
    if (url.startsWith('http://') || url.startsWith('https://')) {
      return url;
    }

    // 构建完整 URL
    const gateway = environment.api.gateway || '';
    const pathSegments = [];

    const gatewayPart = gateway.endsWith('/') ? gateway.slice(0, -1) : gateway;
    if (gatewayPart) {
      pathSegments.push(gatewayPart);
    }

    const urlPart = url.startsWith('/') ? url.slice(1) : url;
    pathSegments.push(urlPart);

    return pathSegments.join('/');
  }

  /**
   * 封装的 fetch 方法，自动添加 gateway 前缀
   *
   * Mock 模式下会检查 SSE_MOCK_REGISTRY 并返回模拟响应
   *
   * @param url - 请求 URL（相对路径或绝对路径）
   * @param init - fetch 请求配置
   * @returns Promise<Response>
   */
  fetch(url: string, init?: RequestInit): Promise<Response> {
    // 1. 处理 Headers (类似 TokenInterceptor)
    const headers = new Headers(init?.headers);

    if (isPlatformBrowser(this.platformId)) {
      const token = localStorage.getItem('auth_token');
      if (token && !headers.has('Authorization')) {
        headers.set('Authorization', `Bearer ${token}`);
      }
    }

    const newInit: RequestInit = {
      ...init,
      headers
    };

    // Mock 模式：检查是否有注册的 SSE mock
    const useMock = environment.useMock;
    const isMockEnabled = typeof useMock === 'boolean' ? useMock : useMock?.enable;

    if (isMockEnabled) {
      const mockHandler = SSE_MOCK_REGISTRY.match(url, init?.method || 'GET');
      if (mockHandler) {
        return this.createMockSseResponse(mockHandler, newInit);
      }
    }

    // 真实模式：调用原生 fetch
    const fullUrl = this.buildFullUrl(url);
    return fetch(fullUrl, newInit);
  }

  /**
   * 创建模拟的 SSE Response 对象
   */
  private createMockSseResponse(mockHandler: (body?: unknown) => unknown[], init?: RequestInit): Promise<Response> {
    return new Promise(resolve => {
      // 解析请求 body
      let requestBody: unknown;
      if (init?.body) {
        try {
          requestBody = typeof init.body === 'string' ? JSON.parse(init.body) : init.body;
        } catch {
          requestBody = init.body;
        }
      }

      // 获取 mock 数据
      const mockData = mockHandler(requestBody);

      // 创建 ReadableStream
      const stream = new ReadableStream({
        start(controller) {
          let index = 0;

          const pushChunk = () => {
            if (index >= mockData.length) {
              controller.close();
              return;
            }

            const chunk = mockData[index++];
            const sseData = `data: ${JSON.stringify(chunk)}\n\n`;
            const encoder = new TextEncoder();
            controller.enqueue(encoder.encode(sseData));

            // 模拟延迟（150ms）
            setTimeout(pushChunk, 150);
          };

          pushChunk();
        }
      });

      // 创建伪造的 Response
      const response = new Response(stream, {
        status: 200,
        headers: {
          'Content-Type': 'text/event-stream',
          'Cache-Control': 'no-cache',
          Connection: 'keep-alive'
        }
      });

      resolve(response);
    });
  }
}
