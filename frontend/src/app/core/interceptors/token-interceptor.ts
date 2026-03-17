import { isPlatformBrowser } from '@angular/common';
import { HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject, PLATFORM_ID } from '@angular/core';
import { Observable } from 'rxjs';

/**
 * 添加认证令牌拦截器 (SSR 安全版)
 */
export const addTokenInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
  const platformId = inject(PLATFORM_ID);

  // SSR 安全检查：如果在服务端运行，直接放行，不访问 localStorage
  if (!isPlatformBrowser(platformId)) {
    return next(req);
  }

  const token = localStorage.getItem('auth_token');

  if (token && !req.headers.has('Authorization')) {
    const newReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
    return next(newReq);
  }

  return next(req);
};
