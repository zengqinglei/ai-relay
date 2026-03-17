import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { RouterOutlet } from '@angular/router';
// 导入所需的 PrimeNG 模块
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { ToastModule } from 'primeng/toast';

import { StartupService } from './core/services/startup-service';
import { ThemeService } from './core/services/theme-service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, ProgressSpinnerModule, CardModule, ButtonModule, ToastModule],
  template: `
    <!-- Toast 消息组件 - 用于显示全局错误和通知 -->
    <p-toast />

    @switch (startupService.status()) {
      @case ('success') {
        <router-outlet></router-outlet>
      }
      @case ('loading') {
        <div class="h-screen w-full flex items-center justify-center bg-surface-ground">
          <p-progressSpinner ariaLabel="正在加载..."></p-progressSpinner>
        </div>
      }
      @case ('failed') {
        <div class="h-screen w-full flex items-center justify-center bg-surface-ground">
          @if (startupService.error(); as error) {
            <p-card header="应用加载失败" [style]="{ width: '360px', textAlign: 'center' }">
              <p>{{ formatHttpError(error) }}</p>
              <ng-template pTemplate="footer">
                <p-button label="重试" icon="pi pi-refresh" (click)="onRetryClick()" [loading]="isRetrying()" [disabled]="isRetrying()">
                </p-button>
              </ng-template>
            </p-card>
          }
        </div>
      }
    }
  `
})
export class App {
  protected readonly startupService = inject(StartupService);
  // 注入 ThemeService 确保主题在应用启动时生效 (通过构造函数中的 effect)
  protected readonly themeService = inject(ThemeService);

  private _isRetrying = signal(false);
  public readonly isRetrying = this._isRetrying.asReadonly();

  async onRetryClick(): Promise<void> {
    this._isRetrying.set(true);
    await this.startupService.retry();
    this._isRetrying.set(false);
  }

  protected formatHttpError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      if (error.error instanceof ErrorEvent) {
        // 客户端或网络错误
        return `客户端错误: ${error.error.message}`;
      } else {
        // 后端返回的错误
        const contentType = error.headers.get('Content-Type');
        if (contentType?.includes('application/json') && error.error?.message) {
          return `请求失败: ${error.error.message} (代码: ${error.error.code})`;
        }
        return `未知服务端错误: ${error.status} - ${error.statusText}`;
      }
    }

    // 处理非 HttpErrorResponse 的其他未知错误
    if (error instanceof Error) {
      return `发生未知错误: ${error.message}`;
    }

    return `发生未知错误，请稍后重试。`;
  }
}
