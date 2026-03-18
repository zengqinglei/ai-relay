import { environmentBase, Environment } from './environment.base';

// 支持构建时环境变量注入 API Gateway 地址
// 占位符会在构建时被替换为实际值
// 使用方式: API_GATEWAY=https://api.example.com npm run build
const apiGateway = '__API_GATEWAY__';

export const environment: Environment = {
  ...environmentBase,
  production: true,
  api: {
    ...environmentBase.api,
    gateway: apiGateway === '__API_GATEWAY__' ? '' : apiGateway
  }
};
