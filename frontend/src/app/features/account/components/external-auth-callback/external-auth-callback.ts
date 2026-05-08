import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { lastValueFrom } from 'rxjs';

import { AuthService } from '../../../../core/services/auth-service';
import { AccountService } from '../../services/account-service';

/**
 * 外部登录回调组件
 *
 * 处理 GitHub/Google 等第三方登录重定向回来后的流程:
 * 1. 从 URL query params 中获取 code、state、provider
 * 2. 将 code+state 发送到后端换取 Cookie session
 * 3. 触发 OIDC authorize 流程（复用 AuthCallback 组件完成最终登录）
 */
@Component({
  selector: 'app-external-auth-callback',
  imports: [CommonModule, ProgressSpinnerModule],
  template: `
    <main class="flex min-h-screen items-center justify-center bg-surface-50 px-4 dark:bg-surface-950">
      <section class="text-center">
        @if (error()) {
          <h1 class="mb-3 text-2xl font-semibold text-red-500">登录失败</h1>
          <p class="text-surface-600 dark:text-surface-300">{{ error() }}</p>
        } @else {
          <p-progress-spinner ariaLabel="正在完成登录" />
          <h1 class="mt-4 text-2xl font-semibold text-surface-900 dark:text-surface-0">正在处理第三方登录</h1>
        }
      </section>
    </main>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ExternalAuthCallback implements OnInit {
  private authService = inject(AuthService);
  private accountService = inject(AccountService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);

  protected readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.processCallback();
  }

  private async processCallback(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    const code = params.get('code');
    const state = params.get('state');
    const provider = params.get('provider') ?? this.route.snapshot.paramMap.get('provider');

    if (!code || !provider) {
      this.error.set('缺少必要的回调参数');
      return;
    }

    try {
      // 1. 将 code+state 发送到后端建立 Cookie session
      await lastValueFrom(
        this.accountService.externalLoginCallback(provider, { provider, code, state: state ?? '' })
      );

      // 2. 加载用户信息并根据角色跳转
      await lastValueFrom(this.authService.loadUser());
      if (this.authService.currentUser()?.isAdmin()) {
        this.router.navigate(['/platform']);
      } else {
        this.router.navigate(['/workspace']);
      }
    } catch (err) {
      console.error('第三方登录回调处理失败', err);
      this.error.set('第三方登录失败，请返回重试');
    }
  }
}
