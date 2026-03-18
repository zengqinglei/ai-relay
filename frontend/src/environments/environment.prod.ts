import { environmentBase, Environment } from './environment.base';

// 支持构建时环境变量注入 API Gateway 地址
// 使用方式: API_GATEWAY=https://api.example.com npm run build
const apiGateway = typeof process !== 'undefined' && process.env?.['API_GATEWAY']
  ? process.env['API_GATEWAY']
  : ''; // 默认空字符串（前后端同域部署）

export const environment: Environment = {
  ...environmentBase,
  production: true,
  api: {
    ...environmentBase.api,
    gateway: apiGateway
  }
};
