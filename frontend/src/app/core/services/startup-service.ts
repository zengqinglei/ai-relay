import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';

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

    const pathname = window.location.pathname;
    const hash = window.location.hash;

    if (pathname.includes('/auth/login') || hash.includes('/auth/login')) {
      this.authService.clearAuthData();
      this._status.set('success');
      return;
    }

    if (
      pathname.includes('/auth/callback') ||
      pathname.includes('/auth/external-callback') ||
      hash.includes('/auth/callback') ||
      hash.includes('/auth/external-callback')
    ) {
      this._status.set('success');
      return;
    }

    if (!this.isProtectedRoute(pathname, hash)) {
      this._status.set('success');
      return;
    }

    try {
      await this.authService.initializeAuth();
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

  private isProtectedRoute(pathname: string, hash: string): boolean {
    const route = hash.startsWith('#/') ? hash.slice(1) : pathname;
    return route.startsWith('/workspace') || route.startsWith('/platform');
  }
}
