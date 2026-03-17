import { environmentBase, Environment } from './environment.base';

export const environment: Environment = {
  ...environmentBase,
  api: {
    ...environmentBase.api,
    gateway: 'http://dev.api.leistd.com'
  }
};
