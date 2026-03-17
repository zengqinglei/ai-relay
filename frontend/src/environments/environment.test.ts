import { environmentBase, Environment } from './environment.base';

export const environment: Environment = {
  ...environmentBase,
  api: {
    ...environmentBase.api,
    gateway: 'http://test.api.leistd.com' // 覆盖网关地址
  }
};
