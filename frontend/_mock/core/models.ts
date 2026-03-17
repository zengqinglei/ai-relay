import { HttpHeaders, HttpRequest } from '@angular/common/http';

export class MockException extends Error {
  constructor(
    public status: number,
    public error?: any
  ) {
    super();
  }
}

export interface MockResponse {
  status?: number;
  headers?: HttpHeaders;
  body?: any;
  /** 模拟延迟，单位：毫秒 */
  delay?: number;
}

export interface MockRequest {
  /** 请求原始对象 */
  readonly original: HttpRequest<any>;
  /** 请求URL */
  readonly url: string;
  /** URL查询参数 */
  readonly queryParams: Record<string, any>;
  /** 请求标头 */
  readonly headers: HttpHeaders;
  /** 请求体 */
  readonly body: any;
  /** URL路由参数 */
  params: any;
}

export interface MockConfig {
  enable: boolean;
  exclude?: string | string[];
  /** 模拟延迟，单位：毫秒 */
  delay?: number;
  /** 是否打印 Mock 日志 */
  log?: boolean;
}
