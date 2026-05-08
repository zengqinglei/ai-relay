import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { StyleClassModule } from 'primeng/styleclass';
import { lastValueFrom } from 'rxjs';

import { environment } from '../../../../../environments/environment';
import { AuthService } from '../../../../core/services/auth-service';
import { LayoutService } from '../../../../layout/services/layout-service';
import { LogoComponent } from '../../../../shared/components/logo/logo';
import { ThemeConfigurator } from '../../../../shared/components/theme-configurator/theme-configurator';
import { AccountService } from '../../services/account-service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    CheckboxModule,
    StyleClassModule,
    ThemeConfigurator,
    LogoComponent
  ],
  templateUrl: './login.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Login {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private authService = inject(AuthService);
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private messageService = inject(MessageService);
  public layoutService = inject(LayoutService);

  // 加载状态
  private _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  // Mock状态
  public readonly isMockEnabled = signal(typeof environment.useMock === 'object' && environment.useMock.enable === true);

  // 登录表单
  loginForm = this.fb.group({
    usernameOrEmail: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(256)]],
    password: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(100)]],
    rememberMe: [false]
  });

  constructor() {
    // 进入登录页面时清理旧的认证信息
    this.authService.clearAuthData();
  }

  /**
   * 提交登录表单
   */
  async onSubmit() {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this._isLoading.set(true);

    try {
      const { usernameOrEmail, password } = this.loginForm.value;

      const loginInput = {
        usernameOrEmail: usernameOrEmail!,
        password: password!
      };
      const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');

      await lastValueFrom(this.authService.login(loginInput));

      if (this.isValidAuthorizationReturnUrl(returnUrl)) {
        window.location.href = returnUrl;
      } else {
        await lastValueFrom(this.authService.loadUser());

        // 登录成功提示
        this.messageService.add({
          severity: 'success',
          summary: '登录成功',
          detail: '欢迎回来！',
          life: 3000
        });

        // 根据角色跳转
        if (this.authService.currentUser()?.isAdmin()) {
          this.router.navigate(['/platform']);
        } else {
          this.router.navigate(['/workspace']);
        }
      }
    } catch (error) {
      // HTTP 错误已经在 httpErrorInterceptor 中统一处理并显示 Toast
      // 这里只需要捕获错误以确保 finally 块能执行，不需要再次抛出
      console.error('登录失败', error);
    } finally {
      this._isLoading.set(false);
    }
  }

  private isValidAuthorizationReturnUrl(returnUrl: string | null): returnUrl is string {
    return returnUrl?.startsWith('/connect/authorize?') === true;
  }

  loginWithGitHub() {
    this.loginWithExternalProvider('github', 'GitHub');
  }

  loginWithGoogle() {
    this.loginWithExternalProvider('google', 'Google');
  }

  /**
   * 通用第三方登录
   */
  private async loginWithExternalProvider(provider: 'github' | 'google', label: string) {
    this._isLoading.set(true);
    try {
      const response = await lastValueFrom(this.accountService.getExternalLoginUrl(provider));

      if (!response.loginUrl) {
        throw new Error('未获取到有效的登录 URL');
      }

      window.location.href = response.loginUrl;
    } catch (error) {
      console.error(`${label} 登录失败`, error);
      this.messageService.add({
        severity: 'error',
        summary: '登录失败',
        detail: `无法连接到 ${label} 登录服务，请稍后重试`,
        life: 3000
      });
      this._isLoading.set(false);
    }
  }

  /**
   * 获取表单字段的错误信息
   */
  getFieldError(fieldName: string): string | null {
    const field = this.loginForm.get(fieldName);
    if (!field || !field.touched || !field.errors) {
      return null;
    }

    if (field.errors['required']) {
      return '此字段不能为空';
    }
    if (field.errors['minlength']) {
      const minLength = field.errors['minlength'].requiredLength;
      return `至少需要 ${minLength} 个字符`;
    }
    return null;
  }
}
