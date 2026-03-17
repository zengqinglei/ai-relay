import { HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { Observable, catchError, tap, throwError } from 'rxjs';

function handleSuccess(_res: HttpResponse<unknown>) {
  // 业务层级错误处理，以下是假定restful有一套统一输出格式（指不管成功与否都有相应的数据格式）情况下进行处理
  // 例如响应内容：
  //  错误内容：{ status: 1, msg: '非法参数' }
  //  正确内容：{ status: 0, response: {  } }
  // 则以下代码片断可直接适用
  // const body = res.body;
  // if (body && body.status !== 0) {
  //   const customError = req.context.get(CUSTOM_ERROR);
  //   if (customError) injector.get(MessageService).error(body.msg);
  //   return customError ? throwError(() => ({ body, _throw: true }) as ReThrowHttpError) : of({});
  // } else {
  //   // 返回原始返回体
  //   if (req.context.get(RAW_BODY) || res.body instanceof Blob) {
  //     return of(res);
  //   }
  //   // 重新修改 `body` 内容为 `response` 内容，对于绝大多数场景已经无须再关心业务状态码
  //   return of(new HttpResponse({ ...res, body: body.response } as any));
  //   // 或者依然保持完整的格式
  //   return of(res);
  // }
}

export const responseInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
  return next(req).pipe(
    tap(event => {
      if (event instanceof HttpResponse) {
        handleSuccess(event);
      }
    }),
    catchError((err: unknown) => {
      // 统一将错误继续抛出，由全局错误处理器统一展示
      return throwError(() => err);
    })
  );
};
