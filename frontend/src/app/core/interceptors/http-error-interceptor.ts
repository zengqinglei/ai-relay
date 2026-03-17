import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../services/auth-service';

/**
 * HTTP 错误拦截器
 *
 * 职责：
 * - 捕获所有 HTTP 请求的错误
 * - 显示用户友好的错误提示
 * - 处理 401 未授权跳转
 * - 重新抛出错误给调用方处理
 */
export const httpErrorInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(MessageService);
  const router = inject(Router);
  const authService = inject(AuthService);

  const CODE_MESSAGES: Record<number, string> = {
    400: '发出的请求有错误，服务器没有进行新建或修改数据的操作。',
    401: '身份验证失败，请重新登录。',
    403: '权限不足，无法访问该资源。',
    404: '发出的请求针对的是不存在的记录，服务器没有进行操作。',
    406: '请求的格式不可得。',
    410: '请求的资源被永久删除，且不会再得到的。',
    422: '当创建一个对象时，发生一个验证错误。',
    500: '服务器发生错误，请检查服务器。',
    502: '网关错误。',
    503: '服务不可用，服务器暂时过载或维护。',
    504: '网关超时。'
  };

  return next(req).pipe(
    catchError((error: unknown) => {
      // 只处理 HTTP 错误
      if (error instanceof HttpErrorResponse) {
        console.error('HTTP Error Interceptor caught error:', error.url, error.status, error.message);

        const contentType = error.headers?.get('Content-Type');

        // 支持 application/json 和 application/problem+json (RFC 7807)
        if ((contentType?.includes('application/json') || contentType?.includes('application/problem+json')) && error.error) {
          const code = error.error.code || '';
          const message = error.error.message || error.error.detail;

          if (message) {
            messageService.add({
              severity: 'error',
              summary: `请求错误（${error.status} - ${code}）`,
              detail: message
            });
          } else {
            const errorText = CODE_MESSAGES[error.status];
            messageService.add({
              severity: 'error',
              summary: `请求错误（${error.status}）`,
              detail: errorText
            });
          }
        } else {
          const errorText = CODE_MESSAGES[error.status] || error.statusText;
          messageService.add({
            severity: 'error',
            summary: `请求错误（${error.status}）`,
            detail: errorText
          });
        }

        // 处理 401 未授权情况
        if (error.status === 401) {
          authService.clearAuthData();
          router.navigate(['/auth/login']);
        }

        // 重新抛出错误，避免下游（如 lastValueFrom）收不到数据直接 complete 导致 EmptyError
        return throwError(() => error);
      }

      // 非 HTTP 错误，继续传播到 GlobalErrorHandler
      throw error;
    })
  );
};
