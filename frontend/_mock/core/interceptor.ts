import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpResponse, HttpErrorResponse } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, of, from, throwError } from 'rxjs';
import { mergeMap, delay, tap, catchError } from 'rxjs/operators';

import { MockException, MockRequest, MockResponse, MockConfig } from './models';
import { environment } from '../../src/environments/environment';

export const MOCK_APIS = new InjectionToken('MOCK_APIS');

@Injectable()
export class MockInterceptor implements HttpInterceptor {
  private apis: any = inject(MOCK_APIS);

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const { url, method, params, headers, body } = req;
    const mockEnv = environment.useMock as MockConfig;

    const matchingRule = this.findMatchingRule(method, url);

    if (!matchingRule) {
      return next.handle(req);
    }

    // Handle exclude rules
    if (mockEnv.exclude) {
      const excludePatterns = Array.isArray(mockEnv.exclude) ? mockEnv.exclude : [mockEnv.exclude];
      for (const pattern of excludePatterns) {
        if (new RegExp(pattern).test(url)) {
          return next.handle(req);
        }
      }
    }

    const mockRequest: MockRequest = {
      original: req,
      url,
      queryParams: params.keys().reduce((acc, key) => ({ ...acc, [key]: params.getAll(key) }), {}),
      headers,
      body,
      params: matchingRule.urlParams
    };

    // Log the request immediately
    if (mockEnv.log) {
      this.log('Mock intercepted', method, url, mockRequest);
    }

    // Wrap handler execution to catch synchronous exceptions
    const result$ = from(
      new Promise((resolve, reject) => {
        try {
          resolve(matchingRule.handler(mockRequest));
        } catch (error) {
          reject(error);
        }
      })
    );

    return result$.pipe(
      mergeMap((result: MockResponse | any) => {
        const response =
          result && typeof result.status !== 'undefined' ? new HttpResponse(result) : new HttpResponse({ status: 200, body: result });

        const delayTime = (result as MockResponse)?.delay ?? mockEnv.delay ?? 0;

        return of(response).pipe(delay(delayTime));
      }),
      tap(response => {
        if (mockEnv.log) {
          this.log('Mock response for', method, url, response, 'log');
        }
      }),
      catchError(error => {
        if (mockEnv.log) {
          this.log('Mock error for', method, url, error, 'error');
        }
        if (error instanceof MockException) {
          // Return HttpErrorResponse with proper headers to trigger GlobalErrorHandler
          const headers = req.headers.set('Content-Type', 'application/json');
          return throwError(
            () =>
              new HttpErrorResponse({
                error: error.error,
                headers: headers,
                status: error.status,
                statusText: 'Mock Error',
                url: req.url
              })
          );
        }
        return throwError(() => error);
      })
    );
  }

  private findMatchingRule(method: string, url: string): { handler: (req: MockRequest) => any; urlParams: any } | null {
    // 移除查询参数，只匹配路径部分
    const urlWithoutQuery = url.split('?')[0];

    // 首先尝试精确匹配（不带参数的路由）
    const exactKey = `${method.toUpperCase()} ${urlWithoutQuery}`;
    const exactRule = this.apis[exactKey];
    if (exactRule) {
      const handler = typeof exactRule === 'function' ? exactRule : () => exactRule;
      return { handler: handler as (req: MockRequest) => any, urlParams: {} };
    }

    // 然后尝试模式匹配（带路径参数的路由，如 /api/users/:id）
    for (const apiPattern in this.apis) {
      const [apiMethod, apiRoute] = apiPattern.split(' ');
      const pattern = new RegExp(`^${apiRoute.replace(/:\w+/g, '([^/]+)')}$`);
      const match = urlWithoutQuery.match(pattern);

      if (apiMethod === method.toUpperCase() && match) {
        const paramNames = (apiRoute.match(/:\w+/g) || []).map(name => name.substring(1));
        const urlParams = paramNames.reduce((acc, name, index) => {
          acc[name] = match[index + 1];
          return acc;
        }, {} as any);

        const matchedRule = this.apis[apiPattern];
        const handler = typeof matchedRule === 'function' ? matchedRule : () => matchedRule;
        return { handler: handler as (req: MockRequest) => any, urlParams };
      }
    }

    return null;
  }

  private log(title: string, method: string, url: string, data: any, level: 'log' | 'error' = 'log'): void {
    const titleStyle = `color: ${level === 'error' ? '#F44336' : '#4CAF50'}; font-weight: bold;`;
    const urlStyle = 'color: #3498db;';

    console.groupCollapsed(`%c ${title}: %c${method} ${url}`, titleStyle, urlStyle);
    console[level](data);
    console.groupEnd();
  }
}
