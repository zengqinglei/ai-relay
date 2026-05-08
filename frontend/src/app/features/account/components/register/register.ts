import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { InputTextModule } from 'primeng/inputtext';
import { PasswordModule } from 'primeng/password';
import { StyleClassModule } from 'primeng/styleclass';
import { TooltipModule } from 'primeng/tooltip';
import { lastValueFrom } from 'rxjs';

import { LayoutService } from '../../../../layout/services/layout-service';
import { LogoComponent } from '../../../../shared/components/logo/logo';
import { ThemeConfigurator } from '../../../../shared/components/theme-configurator/theme-configurator';
import { CaptchaOutputDto, SecurityConfigOutputDto } from '../../models/account.dto';
import { AccountService } from '../../services/account-service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    RouterModule,
    CardModule,
    InputTextModule,
    PasswordModule,
    ButtonModule,
    StyleClassModule,
    ThemeConfigurator,
    LogoComponent,
    TooltipModule
  ],
  templateUrl: './register.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Register implements OnInit {
  private fb = inject(FormBuilder);
  private accountService = inject(AccountService);
  private router = inject(Router);
  private messageService = inject(MessageService);
  private destroyRef = inject(DestroyRef);
  public layoutService = inject(LayoutService);

  private _isLoading = signal(false);
  public readonly isLoading = this._isLoading.asReadonly();

  public securityConfig = signal<SecurityConfigOutputDto | null>(null);
  public captchaData = signal<CaptchaOutputDto | null>(null);

  public countdown = signal(0);
  private countdownIntervalId: ReturnType<typeof setInterval> | null = null;
  private _isSendingEmailCode = signal(false);
  public readonly isSendingEmailCode = this._isSendingEmailCode.asReadonly();

  registerForm = this.fb.group(
    {
      email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
      username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(64), Validators.pattern(/^[a-zA-Z0-9_]+$/)]],
      captchaCode: ['', [Validators.required, Validators.maxLength(10)]],
      emailVerificationCode: [''],
      password: ['', [Validators.required, Validators.pattern(/^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)[\s\S]{8,100}$/)]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: this.passwordMatchValidator }
  );

  private usernameManuallyEdited = false;

  constructor() {
    this.destroyRef.onDestroy(() => this.clearCountdown());
  }

  ngOnInit() {
    this.loadSecurityConfig();
    this.refreshCaptcha();

    // 监听 email 变化，自动推导 username
    this.registerForm
      .get('email')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(email => {
        if (!this.usernameManuallyEdited && email) {
          const usernamePart = email.split('@')[0];
          if (usernamePart) {
            this.registerForm.get('username')?.setValue(usernamePart, { emitEvent: false });
          }
        }
      });

    // 监听 username 变化，标记是否手动修改
    this.registerForm
      .get('username')
      ?.valueChanges.pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(val => {
        if (val) {
          this.usernameManuallyEdited = true;
        }
      });
  }

  passwordMatchValidator(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password')?.value;
    const confirmPassword = control.get('confirmPassword')?.value;

    if (password && confirmPassword && password !== confirmPassword) {
      control.get('confirmPassword')?.setErrors({ passwordMismatch: true });
      return { passwordMismatch: true };
    } else {
      const errors = control.get('confirmPassword')?.errors;
      if (errors) {
        delete errors['passwordMismatch'];
        if (Object.keys(errors).length === 0) {
          control.get('confirmPassword')?.setErrors(null);
        } else {
          control.get('confirmPassword')?.setErrors(errors);
        }
      }
      return null;
    }
  }

  async loadSecurityConfig() {
    try {
      const config = await lastValueFrom(this.accountService.getSecurityConfig());
      this.securityConfig.set(config);
      if (config.enableEmailVerification) {
        this.registerForm.get('emailVerificationCode')?.setValidators([Validators.required]);
        this.registerForm.get('emailVerificationCode')?.updateValueAndValidity();
      }
    } catch (err) {
      console.error('Failed to load security config', err);
    }
  }

  async refreshCaptcha() {
    try {
      const captcha = await lastValueFrom(this.accountService.getCaptcha());
      this.captchaData.set(captcha);
      this.registerForm.get('captchaCode')?.setValue('');
    } catch (err) {
      console.error('Failed to refresh captcha', err);
    }
  }

  async sendEmailCode() {
    const email = this.registerForm.get('email')?.value;
    const captchaCode = this.registerForm.get('captchaCode')?.value;
    const captchaToken = this.captchaData()?.captchaToken;

    if (!email || this.registerForm.get('email')?.invalid) {
      this.messageService.add({ severity: 'warn', summary: '提示', detail: '请先输入有效的邮箱' });
      return;
    }

    if (!captchaCode || !captchaToken) {
      this.messageService.add({ severity: 'warn', summary: '提示', detail: '请先输入图形验证码' });
      return;
    }

    this._isSendingEmailCode.set(true);
    try {
      await lastValueFrom(
        this.accountService.sendEmailCode({
          email,
          captchaCode,
          captchaToken
        })
      );
      this.messageService.add({ severity: 'success', summary: '成功', detail: '验证码已发送，请查收邮件' });
      this.startCountdown();
    } catch {
      this.refreshCaptcha(); // 如果验证码错误，刷新图形验证码
    } finally {
      this._isSendingEmailCode.set(false);
    }
  }

  private startCountdown() {
    this.clearCountdown();
    this.countdown.set(60);
    this.countdownIntervalId = setInterval(() => {
      const current = this.countdown();
      if (current <= 1) {
        this.clearCountdown();
      } else {
        this.countdown.set(current - 1);
      }
    }, 1000);
  }

  private clearCountdown() {
    if (this.countdownIntervalId) {
      clearInterval(this.countdownIntervalId);
      this.countdownIntervalId = null;
    }
    this.countdown.set(0);
  }

  async onSubmit() {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this._isLoading.set(true);
    try {
      const formValue = this.registerForm.value;
      const captchaToken = this.captchaData()?.captchaToken;

      if (!captchaToken) {
        this.messageService.add({ severity: 'error', summary: '错误', detail: '请刷新获取图形验证码' });
        return;
      }

      await lastValueFrom(
        this.accountService.register({
          email: formValue.email!,
          username: formValue.username!,
          password: formValue.password!,
          captchaCode: formValue.captchaCode!,
          captchaToken: captchaToken,
          emailVerificationCode: formValue.emailVerificationCode || undefined
        })
      );

      this.messageService.add({ severity: 'success', summary: '注册成功', detail: '账号创建成功，请登录' });
      this.router.navigate(['/auth/login']);
    } catch {
      this.refreshCaptcha();
    } finally {
      this._isLoading.set(false);
    }
  }

  getFieldError(fieldName: string): string | null {
    const field = this.registerForm.get(fieldName);
    if (!field || !field.touched || !field.errors) {
      return null;
    }

    if (field.errors['required']) {
      return '此字段不能为空';
    }
    if (field.errors['email']) {
      return '邮箱格式不正确';
    }
    if (field.errors['pattern'] && fieldName === 'username') {
      return '只能包含字母、数字和下划线';
    }
    if (field.errors['pattern'] && fieldName === 'password') {
      return '密码需 8-100 位，包含大小写字母及数字';
    }
    if (field.errors['minlength']) {
      const minLength = field.errors['minlength'].requiredLength;
      return `至少需要 ${minLength} 个字符`;
    }
    if (field.errors['passwordMismatch']) {
      return '两次输入的密码不一致';
    }
    return null;
  }
}
