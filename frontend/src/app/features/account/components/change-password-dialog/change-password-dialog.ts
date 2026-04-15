import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, effect, inject, model, signal } from '@angular/core';
import { AbstractControl, FormBuilder, ReactiveFormsModule, ValidationErrors, ValidatorFn, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { PasswordModule } from 'primeng/password';
import { finalize } from 'rxjs/operators';

import { DIALOG_CONFIGS } from '../../../../shared/constants/dialog-config.constants';
import { AccountService } from '../../services/account-service';

const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,20}$/;

function passwordRulesValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const group = control;
    const currentPassword = String(group.get('currentPassword')?.value ?? '');
    const newPassword = String(group.get('newPassword')?.value ?? '');
    const confirmPassword = String(group.get('confirmPassword')?.value ?? '');

    const errors: Record<string, true> = {};

    if (currentPassword && newPassword && currentPassword === newPassword) {
      errors['sameAsCurrent'] = true;
    }

    if (confirmPassword && newPassword !== confirmPassword) {
      errors['passwordMismatch'] = true;
    }

    return Object.keys(errors).length > 0 ? errors : null;
  };
}

@Component({
  selector: 'app-change-password-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DialogModule, ButtonModule, PasswordModule],
  templateUrl: './change-password-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChangePasswordDialogComponent {
  readonly visible = model(false);

  private readonly fb = inject(FormBuilder);
  private readonly accountService = inject(AccountService);
  private readonly messageService = inject(MessageService);

  readonly dialogConfig = DIALOG_CONFIGS.SMALL;
  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group(
    {
      currentPassword: ['', [Validators.required]],
      newPassword: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
      confirmPassword: ['', [Validators.required]]
    },
    { validators: [passwordRulesValidator()] }
  );

  constructor() {
    effect(() => {
      if (this.visible()) {
        this.form.reset({
          currentPassword: '',
          newPassword: '',
          confirmPassword: ''
        });
      }
    });
  }

  hasPasswordRuleError(): boolean {
    const control = this.form.controls.newPassword;
    return control.touched && control.hasError('pattern');
  }

  shouldShowMismatchError(): boolean {
    return this.form.hasError('passwordMismatch') && (this.form.controls.confirmPassword.touched || this.form.controls.newPassword.touched);
  }

  shouldShowSamePasswordError(): boolean {
    return this.form.hasError('sameAsCurrent') && (this.form.controls.currentPassword.touched || this.form.controls.newPassword.touched);
  }

  onHide(): void {
    this.visible.set(false);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);

    this.accountService
      .changePassword(this.form.getRawValue())
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '密码已更新' });
          this.visible.set(false);
        }
      });
  }
}
