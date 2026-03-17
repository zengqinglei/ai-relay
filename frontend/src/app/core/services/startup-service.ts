import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, signal, inject } from '@angular/core';
import { lastValueFrom } from 'rxjs';

import { AuthService } from './auth-service';

export type StartupStatus = 'loading' | 'success' | 'failed';

@Injectable({ providedIn: 'root' })
export class StartupService {
  private authService = inject(AuthService);

  private _status = signal<StartupStatus>('loading');
  private _error = signal<unknown | null>(null);

  public readonly status = this._status.asReadonly();
  public readonly error = this._error.asReadonly();

  async load(): Promise<void> {
    this._status.set('loading');
    this._error.set(null);

    // 如果当前是登录页，直接清理 Token 并跳过加载
    if (window.location.hash.includes('/auth/login')) {
      this.authService.clearAuthData();
      this._status.set('success');
      return;
    }

    if (!this.authService.isAuthenticated()) {
      this._status.set('success');
      return;
    }

    try {
      await lastValueFrom(this.authService.loadUser());
      this._status.set('success');
    } catch (err: unknown) {
      if (err instanceof HttpErrorResponse && err.status === 401) {
        this.authService.clearAuthData();
        this._status.set('success');
      } else {
        this._error.set(err);
        this._status.set('failed');
      }
    }
  }

  async retry(): Promise<void> {
    // Signals 会自动处理UI更新，我们不再需要手动延时
    await this.load();
  }
}
