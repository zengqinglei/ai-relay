import { MockConfig } from '../../_mock/core/models';

export interface Environment {
  production: boolean;
  useHash: boolean;
  /**
   * 是否启用 Mock 服务，仅在非生产环境有效。
   * - `false`: 关闭 Mock
   * - `true`: 开启所有 Mock
   * - `object`: 按模块开启 Mock (特性开关)
   */
  useMock: boolean | MockConfig;
  api: {
    gateway: string; // 网关地址, 为空则不使用（服务地址为完整地址）
    authService: {
      url: string;
      refreshTokenEnabled?: boolean;
      refreshTokenType?: string;
    };
    appService: {
      url: string;
    };
    envService: {
      url: string;
    };
  };
}

// 这是所有环境共享的基础配置
export const environmentBase: Environment = {
  production: false,
  useHash: true,
  useMock: false, // 默认关闭
  api: {
    gateway: 'https://example.com', // 本地开发的网关地址
    authService: {
      url: '/auth-service',
      refreshTokenEnabled: true,
      refreshTokenType: 'auth-refresh'
    },
    appService: {
      url: '/app-service'
    },
    envService: {
      url: '/env-service'
    }
  }
};
