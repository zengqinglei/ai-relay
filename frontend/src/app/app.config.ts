import { provideHttpClient, withInterceptors, withInterceptorsFromDi } from '@angular/common/http';
import {
  ApplicationConfig,
  ErrorHandler,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
  provideZonelessChangeDetection
} from '@angular/core';
import {
  provideRouter,
  RouterFeatures,
  withComponentInputBinding,
  withEnabledBlockingInitialNavigation,
  withHashLocation,
  withInMemoryScrolling,
  withViewTransitions
} from '@angular/router';
import Aura from '@primeuix/themes/aura';
import { MessageService } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';

import { routes } from './app.routes';
import { GlobalErrorHandler } from './core/handlers/global-error-handler';
import { httpErrorInterceptor } from './core/interceptors/http-error-interceptor';
import { addTokenInterceptor } from './core/interceptors/token-interceptor';
import { provideMock } from '../../_mock/core/providers';
import { environment } from '../environments/environment';
import { urlFormatInterceptor } from './core/interceptors/url-format-interceptor';
import { StartupService } from './core/services/startup-service';

// 定义路由特性，用于增强应用功能和用户体验
const routerFeatures: RouterFeatures[] = [
  // 启用路由参数到组件输入的自动绑定
  withComponentInputBinding(),
  // 启用基于浏览器 View Transitions API 的页面过渡动画
  withViewTransitions(),
  // 配置导航时的滚动行为，导航后滚动到页面顶部
  withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }),
  withEnabledBlockingInitialNavigation(),
  // 根据环境配置决定是否启用哈希路由
  ...(environment.useHash ? [withHashLocation()] : [])
];

export const appConfig: ApplicationConfig = {
  providers: [
    provideZonelessChangeDetection(),
    // 注册全局错误监听器
    provideBrowserGlobalErrorListeners(),
    // 注册全局错误处理器，替换 Angular 默认的 ErrorHandler
    { provide: ErrorHandler, useClass: GlobalErrorHandler },
    provideRouter(routes, ...routerFeatures),
    providePrimeNG({
      theme: {
        preset: Aura,
        options: {
          darkModeSelector: '.dark',
          cssLayer: {
            name: 'primeng',
            order: 'theme, base, primeng'
          }
        }
      }
    }),
    provideHttpClient(
      withInterceptors([
        urlFormatInterceptor,
        addTokenInterceptor,
        httpErrorInterceptor // 捕获所有 HTTP 错误并显示用户提示
      ]),
      withInterceptorsFromDi() // 启用对基于类的拦截器的支持
    ),
    // 在应用初始化时加载关键数据
    provideAppInitializer(() => inject(StartupService).load()),
    // 注册 Mock 服务
    ...provideMock(environment.useMock),
    // 注册 PrimeNG MessageService
    MessageService
  ]
};
