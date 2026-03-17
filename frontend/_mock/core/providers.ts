import { HTTP_INTERCEPTORS } from '@angular/common/http';
import { Provider } from '@angular/core';

import { MOCK_APIS, MockInterceptor } from './interceptor';
import { MockConfig } from './models';
import * as allApis from '../index';

/**
 * 提供 Mock 服务的核心函数。
 *
 * @param config Mock 配置对象，通常来自环境文件。
 * @returns 返回一个 Provider 数组，可直接在 app.config.ts 的 providers 中使用。
 */
export function provideMock(config: boolean | MockConfig): Provider[] {
  if (typeof config === 'boolean' && !config) {
    return [];
  }

  if (typeof config === 'object' && !config.enable) {
    return [];
  }

  // 动态地将所有导入的 *_API 对象合并到一个 APIS 对象中
  const apis = Object.values(allApis)
    .filter(value => typeof value === 'object' && value !== null)
    .reduce((acc, current) => ({ ...acc, ...current }), {});

  return [
    { provide: MOCK_APIS, useValue: apis },
    { provide: HTTP_INTERCEPTORS, useClass: MockInterceptor, multi: true }
  ];
}
