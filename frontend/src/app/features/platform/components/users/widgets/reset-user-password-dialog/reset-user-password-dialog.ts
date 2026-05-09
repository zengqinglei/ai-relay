import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, model, output } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { PasswordModule } from 'primeng/password';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { ResetUserPasswordInputDto } from '../../../../models/user-management.dto';

const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,20}$/;

@Component({
  selector: 'app-reset-user-password-dialog',
  imports: [CommonModule, ReactiveFormsModule, DialogModule, ButtonModule, PasswordModule],
  templateUrl: './reset-user-password-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ResetUserPasswordDialogComponent {
  visible = model(false);
  saving = model(false);
  readonly saved = output<ResetUserPasswordInputDto>();

  private readonly fb = inject(FormBuilder);

  dialogConfig = DIALOG_CONFIGS.SMALL;
  readonly form = this.fb.nonNullable.group({
    password: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]]
  });

  hasPasswordRuleError() {
    const control = this.form.controls.password;
    return control.touched && control.hasError('pattern');
  }

  onHide() {
    this.visible.set(false);
    this.form.reset({ password: '' });
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.saved.emit({ password: this.form.controls.password.value });
  }
}
