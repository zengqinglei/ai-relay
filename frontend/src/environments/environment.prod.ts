import { environmentBase, Environment } from './environment.base';

export const environment: Environment = {
  ...environmentBase,
  production: true,
  api: {
    ...environmentBase.api,
    gateway: '' // 使用相对路径，前后端同域部署时无需指定网关地址
  }
};
