import { Pipe, PipeTransform } from '@angular/core';

/**
 * HTTP 状态码转 PrimeNG Severity Pipe
 * 将 HTTP 状态码映射为 PrimeNG Tag/Message 组件的 severity 属性
 */
@Pipe({
  name: 'httpStatusSeverity',
  standalone: true
})
export class HttpStatusSeverityPipe implements PipeTransform {
  transform(status: number | undefined | null): 'success' | 'warn' | 'danger' | 'info' {
    if (!status) return 'info';

    if (status >= 200 && status < 300) {
      return 'success'; // 2xx - 成功
    }

    if (status >= 300 && status < 400) {
      return 'warn'; // 3xx - 重定向
    }

    return 'danger'; // 4xx/5xx - 客户端错误或服务器错误
  }
}
