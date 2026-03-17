import { environmentBase, Environment } from './environment.base';

export const environment: Environment = {
  ...environmentBase,
  production: true,
  api: {
    ...environmentBase.api,
    gateway: 'http://prod.api.leistd.com' // 覆盖网关地址
  }
};
