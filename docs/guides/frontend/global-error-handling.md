# 指南：全局错误处理

本文档详细阐述了前端项目中基于 Angular 内置 `ErrorHandler` 实现的集中式错误处理机制，旨在确保所有未被捕获的异常都能得到统一、友好的处理。

## 核心流程

1.  **HTTP 拦截器 (`response-interceptor`)**: 此拦截器捕获所有 HTTP 响应。其唯一职责是检查响应状态，若发生错误，则直接将原始的 `HttpErrorResponse` **重新抛出**。它本身不执行任何 UI 提示或错误终结操作。

2.  **业务代码 (优先处理)**: 在业务服务或组件中发起的 API 请求，可以通过 RxJS 的 `catchError` 操作符优先捕获并处理特定异常。

3.  **全局处理器 (`GlobalErrorHandler`)**: 这是一个自定义的、实现了 `ErrorHandler` 接口的类。它作为应用的“最终防线”，接收所有未被业务代码“消化”的未捕获异常。

4.  **统一展示**: 在 `GlobalErrorHandler` 内部，系统会判断错误是否为 `HttpErrorResponse`。如果是，则调用 PrimeNG 的 `MessageService` 来显示一个全局的、统一风格的错误提示。

## 业务代码如何处理异常

开发者可以根据业务需求，选择是否对某个 API 请求的错误进行个性化处理。

### 场景一：让全局处理器统一提示 (默认行为)

如果某个请求的失败不需要特殊的界面交互，只需简单通知用户即可。在这种情况下，业务代码**无需**添加 `catchError`。错误将自动冒泡至 `GlobalErrorHandler` 进行统一处理。

```typescript
// in user.service.ts
deleteUser(userId: string): Observable<any> {
  // 无需 catchError，错误会自动冒泡到 GlobalErrorHandler
  return this.http.delete(`/api/v1/users/${userId}`);
}
```

### 场景二：业务侧自定义处理，并阻止全局提示

当希望在界面上显示一个内联的、更具体的错误提示（例如“用户名已存在”），而不是通用的全局弹窗时，必须在业务代码中“捕获并消化”该错误。

```typescript
// in auth.service.ts
import { of } from 'rxjs';
import { catchError } from 'rxjs/operators';

checkUsername(username: string): Observable<{ exists: boolean } | null> {
  return this.http.post<{ exists: boolean }>('/api/v1/auth/check-username', { username }).pipe(
    catchError(err => {
      // 在此执行特定于业务的逻辑，
      // 例如，通过一个 Subject 通知组件显示内联错误信息。
      console.warn('用户名检查失败，已在业务层处理', err);

      // **关键**: 返回一个成功的、值为 null 的 Observable。
      // 这会“消化”掉错误，使其不再向外传播，因此 GlobalErrorHandler 不会被触发。
      return of(null);
    })
  );
}