import { HttpContextToken, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

/**
 * 定义一个上下文令牌，用于在请求中标记是是否传递服务在网关中的名字。
 */
export const GATEWAY_SERVICE_NAME = new HttpContextToken<string>(() => '');

/**
 * URL格式化拦截器。
 * 自动为请求URL添加网关和微服务前缀。
 */
export const urlFormatInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
  let url = req.url;

  // If Mock is enabled, do not prepend gateway, let MockInterceptor handle the relative URL
  const useMock = environment.useMock;
  const isMockEnabled = typeof useMock === 'boolean' ? useMock : useMock?.enable;

  if (isMockEnabled) {
    return next(req);
  }

  const gateway = environment.api.gateway || '';
  const gatewayServiceName = req.context.get(GATEWAY_SERVICE_NAME) || '';

  if (!url.startsWith('https') && !url.startsWith('http')) {
    const pathSegments = [];
    const gatewayPart = gateway.endsWith('/') ? gateway.slice(0, -1) : gateway;
    if (gatewayPart) {
      pathSegments.push(gatewayPart);
    }
    const servicePart = gatewayServiceName.startsWith('/') ? gatewayServiceName.slice(1) : gatewayServiceName;
    if (servicePart) {
      pathSegments.push(servicePart);
    }
    const urlPart = url.startsWith('/') ? url.slice(1) : url;
    pathSegments.push(urlPart);
    url = pathSegments.join('/');
  }

  const newReq = req.clone({ url });
  return next(newReq);
};
