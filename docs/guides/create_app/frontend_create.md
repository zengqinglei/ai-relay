# 🚀 前端项目初始化

本文档为基于 Angular, PrimeNG 和 Tailwind CSS 的前端项目提供标准化初始化模板。

## 🎯 核心技术栈

- **前端框架**: Angular v20+
- **UI 组件库**: PrimeNG v20+
- **原子化CSS**: Tailwind CSS v4+
- **命令行工具**: Angular CLI v20+
- **开发语言**: TypeScript 5.8+
- **状态管理**: Angular Signals (用于组件级状态)

---

## 🛠️ 初始化流程

标准化的初始化流程确保项目起点一致。
1.  **更新 Angular CLI**
    ```bash
    npm update -g @angular/cli
    ```

2.  **创建项目**
    ```bash
    ng new leistd-devops-web --style css --routing --directory frontend
    ```

3.  **集成依赖**
    ```bash
    cd frontend
    npm install primeng@20.0.0-rc.3 @primeuix/themes primeicons tailwindcss @tailwindcss/postcss postcss tailwindcss-primeui
    ```

4.  **配置 PostCSS**

    创建 `.postcssrc.json` 文件：
    ```json
    {
      "plugins": {
        "@tailwindcss/postcss": {}
      }
    }
    ```

5.  **配置样式与主题**

    - 在 `src/styles.css` 中引入 Tailwind 和 PrimeNG 主题：
      ```css
      @import "tailwindcss";
      @plugin "tailwindcss-primeui";
      ```
    - 在 `src/app/app.config.ts` 中配置 PrimeNG 主题：
      ```typescript
      import { ApplicationConfig } from '@angular/core';
      import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
      import { providePrimeNG } from 'primeng/config';
      import Aura from '@primeuix/themes/aura';

      export const appConfig: ApplicationConfig = {
        providers: [
          provideAnimationsAsync(),
          providePrimeNG({
            theme: {
              preset: Aura,
              options: {
                cssLayer: { name: 'primeng', order: 'theme, base, primeng' }
              }
            }
          })
        ]
      };
      ```

6.  **项目清理**
    - 修改 `src/index.html` 的语言为 `<html lang="zh">`。
    - 清理 `src/app/` 下的 `app.component.html`, `app.component.css`, `app.component.spec.ts`。
    - 更新 `src/app/app.ts` 为根路由出口：
      ```typescript
      import { Component, signal } from '@angular/core';
      import { RouterOutlet } from '@angular/router';

      @Component({
        selector: 'app-root',
        imports: [RouterOutlet],
        template: '<router-outlet />'
      })
      export class App {
        protected readonly title = signal('leistd-devops-web');
      }
      ```

---

## ⚙️ 多环境配置

为项目预置多环境支持

- **创建环境基础文件**

    在 `src/environments/` 目录下创建 `environment.base.ts`，定义所有环境共享的配置：

    ```typescript
    export interface Environment {
      production: boolean;
      useHash: boolean;
      /**
       * 是否启用 Mock 服务，仅在非生产环境有效。
       * - `false`: 关闭 Mock
       * - `true`: 开启 Mock
       */
      mock: boolean;
      api: {
        geteway: string; // 网关地址, 为空则不使用（服务地址为完整地址）
        /** ... 其他服务
         * authService: {
         *   url: string;
         *   refreshTokenEnabled?: boolean;
         *   refreshTokenType?: string;
         * };
         * appService: {
         *   url: string;
         * };
         * envService: {
         *   url: string;
         * };
         */
      };
    }

    export const environmentBase: Environment = {
      production: false,
      useHash: true,
      mock: false, // 默认关闭
      api: {
        geteway: 'http://localhost:8080', // 本地开发网关
      }
    };
    ```
    同时，创建 `environment.dev.ts`, `environment.prod.ts` 等文件继承并覆盖基础配置。

- **配置 `angular.json`**

    为不同环境配置 `fileReplacements`，以实现构建时自动替换环境文件：

    ```json
    "build": {
      "configurations": {
        "production": {
          "fileReplacements": [
            {
              "replace": "src/environments/environment.ts",
              "with": "src/environments/environment.prod.ts"
            }
          ]
        },
        "debug": {
          "optimization": false,
          "extractLicenses": false,
          "sourceMap": true,
          "fileReplacements": [
            {
              "replace": "src/environments/environment.ts",
              "with": "src/environments/environment.debug.ts"
            }
          ]
        },
        "dev": {
          "fileReplacements": [
            {
              "replace": "src/environments/environment.ts",
              "with": "src/environments/environment.dev.ts"
            }
          ]
        },
        "test": {
          "fileReplacements": [
            {
              "replace": "src/environments/environment.ts",
              "with": "src/environments/environment.test.ts"
            }
          ]
        },
        "uat": {
          "fileReplacements": [
            {
              "replace": "src/environments/environment.ts",
              "with": "src/environments/environment.uat.ts"
            }
          ]
        }
      }
    },
    "serve": {
      "configurations": {
        "debug": {
          "buildTarget": "leistd-devops-web:build:debug"
        },
        "dev": {
          "buildTarget": "leistd-devops-web:build:dev"
        },
        "test": {
          "buildTarget": "leistd-devops-web:build:test"
        },
        "uat": {
          "buildTarget": "leistd-devops-web:build:uat"
        }
      },
      "defaultConfiguration": "debug"
    }
    ```

- **配置 `package.json` 脚本**

    ```json
    "scripts": {
      "start": "ng serve -c debug",
      "start:dev": "ng serve -c dev",
      "start:prod": "ng serve -c production",
      "build:dev": "ng build -c dev",
      "build:prod": "ng build -c production"
    },
    ```

---

## 🔌 HTTP 拦截器配置

- **创建拦截器**

    在 `src/app/core/net/` 目录下创建以下拦截器：

    -   **`url-format.interceptor.ts`**: 自动拼接网关和微服务路径。
        ```typescript
        import { HttpContextToken, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
        import { environment } from '../../../environments/environment';
        import { Observable } from 'rxjs';

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
          const gateway = environment.api.geteway || '';
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
        ```

    -   **`add-token.interceptor.ts`**: 自动为请求添加认证头。
        ```typescript
        import { HttpEvent, HttpHandlerFn, HttpHeaders, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
        import { Observable } from 'rxjs';

        /**
        * 为请求添加额外的HTTP头。
        * @param headers 原始请求头
        * @returns 添加了认证令牌的新请求头
        */
        function getAdditionalHeaders(headers: HttpHeaders): { [name: string]: string } {
          const newHeaders: { [name: string]: string } = {};
          const token = localStorage.getItem('auth_token');
          if (token && !headers.has('Authorization')) {
            newHeaders['Authorization'] = `Bearer ${token}`;
          }
          return newHeaders;
        }

        /**
        * 添加认证令牌拦截器。
        */
        export const addTokenInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
          const newReq = req.clone({
            setHeaders: getAdditionalHeaders(req.headers)
          });
          return next(newReq);
        };
        ```

    -   **`response.interceptor.ts`**: 统一处理成功与失败的响应，并提供全局错误提示。
        ```typescript
        import { HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest, HttpResponse } from '@angular/common/http';
        import { inject } from '@angular/core';
        import { Observable, catchError, tap, throwError } from 'rxjs';
        import { MessageService } from 'primeng/api';

        function handleSuccess(res: HttpResponse<unknown>) {
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

        function handleError(err: HttpErrorResponse): void {
          const messageService = inject(MessageService);
          const contentType = err.headers.get('Content-Type');
          if (contentType?.includes('application/json') && err.error?.code && err.error?.message) {
            // 后端返回的异常处理
            const code = err.error.code;
            const message = err.error.message;
            messageService.add({ severity: 'error', summary: `请求错误（${err.status}）`, detail: `${code} -- ${message}` });
          }
          else {
            // 其他异常处理
            const CODEMESSAGE: Record<number, string> = {
              200: '服务器成功返回请求的数据。',
              201: '新建或修改数据成功。',
              202: '一个请求已经进入后台排队（异步任务）。',
              204: '删除数据成功。',
              400: '发出的请求有错误，服务器没有进行新建或修改数据的操作。',
              401: '用户没有权限（令牌、用户名、密码错误）。',
              403: '用户得到授权，但是访问是被禁止的。',
              404: '发出的请求针对的是不存在的记录，服务器没有进行操作。',
              406: '请求的格式不可得。',
              410: '请求的资源被永久删除，且不会再得到的。',
              422: '当创建一个对象时，发生一个验证错误。',
              500: '服务器发生错误，请检查服务器。',
              502: '网关错误。',
              503: '服务不可用，服务器暂时过载或维护。',
              504: '网关超时。'
            };
            const errortext = CODEMESSAGE[err.status] || err.statusText;
            messageService.add({ severity: 'error', summary: `未知错误（${err.status}）`, detail: `${err.url} ${errortext}` });
          }
        }

        export const responseInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next: HttpHandlerFn): Observable<HttpEvent<unknown>> => {
          return next(req).pipe(
            tap(event => {
              if (event instanceof HttpResponse) {
                handleSuccess(event);
              }
            }),
            catchError((err: unknown) => {
              if (err instanceof HttpErrorResponse) {
                handleError(err);
              }
              // 将原始错误继续抛出
              return throwError(() => err);
            })
          );
        };
        ```

- **配置全局注册拦截器**

    在 `src/app/app.config.ts` 中使用 `provideHttpClient` 和 `withInterceptors` 进行注册。

    ```typescript
    import { provideHttpClient, withInterceptors } from '@angular/common/http';
    import { urlFormatInterceptor } from './core/net/url-format.interceptor';
    import { addTokenInterceptor } from './core/net/add-token.interceptor';
    import { responseInterceptor } from './core/net/response.interceptor';
    
    export const appConfig: ApplicationConfig = {
      providers: [
        // ...其他 providers
        provideHttpClient(withInterceptors([urlFormatInterceptor, addTokenInterceptor, responseInterceptor]))
      ]
    };
    ```

---

## ✨ 路由增强

为了改善用户体验，在 `src/app/app.config.ts` 中预置了以下路由特性：

```typescript
import { environment } from '../environments/environment';
import {
  provideRouter,
  RouterFeatures,
  withComponentInputBinding,
  withHashLocation,
  withInMemoryScrolling,
  withViewTransitions
} from '@angular/router';

// 定义路由特性
const routerFeatures: RouterFeatures[] = [
  withComponentInputBinding(), // 启用路由参数到组件输入的自动绑定
  withViewTransitions(),       // 启用页面过渡动画
  withInMemoryScrolling({ scrollPositionRestoration: 'top' }), // 导航后滚动到顶部
  ...(environment.useHash ? [withHashLocation()] : []) // 根据环境配置决定是否启用哈希路由
];

export const appConfig: ApplicationConfig = {
  providers: [
    // ...
    provideRouter(routes, ...routerFeatures),
  ]
};
```

---

## 📄 README.md 模板

为了提供清晰的项目指引，请使用以下内容覆盖 `frontend/README.md` 文件的默认内容：

````markdown
# 前端项目

本项目基于 Angular、PrimeNG 和 Tailwind CSS 构建。

## 🎯 核心技术栈

- **前端框架**: Angular v20+
- **UI 组件库**: PrimeNG v20+
- **原子化CSS**: Tailwind CSS v4+
- **命令行工具**: Angular CLI v20+
- **开发语言**: TypeScript 5.8+
- **状态管理**: Angular Signals

有关详细的开发规范、目录结构和编码准则，请参阅项目根目录下的 [`.kilocode/rules/frontend-develop.md`](../.kilocode/rules/frontend-develop.md) 文件。

---

## 🚀 快速启动

### 1. 安装依赖

在首次克隆项目后，请先安装所有必需的依赖项：

```bash
npm install
```

### 2. 启动开发服务器

本项目已预置了多套环境配置，您可以根据需要启动对应的开发服务器。

- **启动本地调试环境** (环境配置默认指向 `http://localhost:8080` 网关):
  ```bash
  npm start
  ```

- **启动开发环境** (环境配置默认指向 `http://dev.api.leistd.com` 网关):
  ```bash
  npm run start:dev
  ```

- **启动其他环境**:
  ```bash
  npm run start:test   # 测试环境
  npm run start:uat    # UAT 环境
  npm run start:prod   # 生产环境
  ```

服务器启动后，请在浏览器中打开 `http://localhost:4200/`。应用支持热重载，任何对源文件的修改都会自动刷新页面。

---

## 🛠️ 构建项目

您可以根据目标环境构建项目。构建产物将存放在 `dist/` 目录下。

- **构建开发环境包**:
  ```bash
  npm run build:dev
  ```

- **构建生产环境包** (已优化性能):
  ```bash
  npm run build:prod
  ```

---

## ✅ 运行测试

- **单元测试**:
  ```bash
  npm test
  ```

- **端到端 (E2E) 测试**:
  ```bash
  ng e2e
  ```
  > **注意**: 项目默认未集成 E2E 测试框架，您可根据需要自行添加。

---

## ✨ 代码脚手架

使用 Angular CLI 可以快速生成各种类型的文件。

- **生成新组件**:
  ```bash
  ng generate component your-component-name
  ```

- **查看更多可用选项**:
  ```bash
  ng generate --help
  ```

## 📚 更多资源

- **Angular CLI**: 访问 [Angular CLI 官方文档](https://angular.dev/tools/cli) 获取更详细的命令参考。
- **PrimeNG**: 访问 [PrimeNG 官方文档](https://primeng.org/) 查看所有可用组件。
- **Tailwind CSS**: 访问 [Tailwind CSS 官方文档](https://tailwindcss.com/docs) 学习其原子化类。
````

---