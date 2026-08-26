import { environmentBase, Environment } from './environment.base';

/**
 * GitHub Pages 预览环境。
 *
 * 纯静态托管，没有后端可用，因此全量开启 Mock、网关置空。
 * 这个文件必须提交进仓库并参与类型检查 —— 不要再回到「CI 里用 heredoc
 * 现生成 environment.debug.ts」的做法，那会让环境定义脱离编译器和 review。
 */
export const environment: Environment = {
  ...environmentBase,
  useMock: {
    enable: true,
    exclude: '',
    delay: 500,
    log: false
  },
  api: {
    ...environmentBase.api,
    gateway: ''
  }
};
