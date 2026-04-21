import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';

import { SSE_MOCK_REGISTRY } from '../../../../_mock/core/sse-mock-registry';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class NativeFetchService {
  private readonly platformId = inject(PLATFORM_ID);

  private buildFullUrl(url: string): string {
    if (url.startsWith('http://') || url.startsWith('https://')) {
      return url;
    }

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

  fetch(url: string, init?: RequestInit): Promise<Response> {
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

    const useMock = environment.useMock;
    const isMockEnabled = typeof useMock === 'boolean' ? useMock : useMock?.enable;

    if (isMockEnabled) {
      const method = init?.method || 'GET';
      const mockHandler = SSE_MOCK_REGISTRY.match(url, method);
      if (mockHandler) {
        return this.createMockSseResponse(mockHandler, newInit, url, method);
      }
    }

    const fullUrl = this.buildFullUrl(url);
    return fetch(fullUrl, newInit);
  }

  private createMockSseResponse(
    mockHandler: (body?: unknown, context?: { url: string; method: string; headers?: Headers }) => unknown[],
    init: RequestInit | undefined,
    url: string,
    method: string
  ): Promise<Response> {
    return new Promise(resolve => {
      let requestBody: unknown;
      if (init?.body) {
        try {
          requestBody = typeof init.body === 'string' ? JSON.parse(init.body) : init.body;
        } catch {
          requestBody = init.body;
        }
      }

      const mockData = mockHandler(requestBody, {
        url,
        method,
        headers: init?.headers instanceof Headers ? init.headers : new Headers(init?.headers)
      });

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
            setTimeout(pushChunk, 90);
          };

          pushChunk();
        }
      });

      resolve(
        new Response(stream, {
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            Connection: 'keep-alive'
          }
        })
      );
    });
  }
}
