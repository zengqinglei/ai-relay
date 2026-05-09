import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, input, model, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { FileSelectEvent, FileUploadModule } from 'primeng/fileupload';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { PasswordModule } from 'primeng/password';
import { ToggleSwitchModule } from 'primeng/toggleswitch';

import { DialogLoadingComponent } from '../../../../../../shared/components/dialog-loading/dialog-loading';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { CreateUserInputDto, UpdateUserInputDto, UserManagementOutputDto } from '../../../../models/user-management.dto';

const MAX_AVATAR_SIZE = 1024 * 1024;
const PASSWORD_RULE = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,20}$/;

@Component({
  selector: 'app-user-edit-dialog',
  imports: [
    CommonModule,
    ReactiveFormsModule,
    DialogModule,
    ButtonModule,
    AvatarModule,
    FileUploadModule,
    InputTextModule,
    PasswordModule,
    ToggleSwitchModule,
    MultiSelectModule,
    DividerModule,
    DialogLoadingComponent
  ],
  templateUrl: './user-edit-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UserEditDialogComponent {
  visible = model(false);
  loading = input(false);
  saving = input(false);
  user = input<UserManagementOutputDto | null>(null);
  readonly saved = output<CreateUserInputDto | UpdateUserInputDto>();

  private readonly fb = inject(FormBuilder);

  dialogConfig = DIALOG_CONFIGS.SMALL;
  readonly avatarPreview = signal('');
  readonly displayName = computed(() => this.form.controls.displayName.value.trim() || this.form.controls.username.value.trim() || '未命名用户');
  readonly avatarLabel = computed(() => (this.displayName().trim().charAt(0) || 'U').toUpperCase());
  readonly avatarStyle = computed(() => {
    const seed = (this.form.controls.username.value || this.displayName()).trim();
    let total = 0;

    for (const char of seed) {
      total += char.charCodeAt(0);
    }

    const palette = [
      { background: '#dbeafe', color: '#1d4ed8' },
      { background: '#dcfce7', color: '#15803d' },
      { background: '#fef3c7', color: '#b45309' },
      { background: '#fce7f3', color: '#be185d' },
      { background: '#ede9fe', color: '#6d28d9' }
    ];

    return palette[total % palette.length];
  });
  readonly form = this.fb.nonNullable.group({
    username: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(64), Validators.pattern(/^[a-zA-Z0-9_]+$/)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    displayName: ['', [Validators.maxLength(128)]],
    avatar: [''],
    password: ['', [Validators.required, Validators.pattern(PASSWORD_RULE)]],
    isActive: [true],
    isEmailVerified: [false],
    roles: [['Member'], [Validators.required]]
  });

  roleOptions = [
    { label: '管理员', value: 'Admin' },
    { label: '运营人员', value: 'Operator' },
    { label: '普通成员', value: 'Member' }
  ];

  constructor() {
    effect(() => {
      const user = this.user();
      const avatar = user?.avatar ?? '';
      this.form.reset({
        username: user?.username ?? '',
        email: user?.email ?? '',
        displayName: user?.displayName ?? '',
        avatar,
        password: '',
        isActive: user?.isActive ?? true,
        isEmailVerified: user?.isEmailVerified ?? false,
        roles: user ? [...user.roles] : ['Member']
      });
      this.avatarPreview.set(avatar);

      if (user) {
        this.form.controls.username.disable();
        this.form.controls.password.disable();
      } else {
        this.form.controls.username.enable();
        this.form.controls.password.enable();
      }
    });
  }

  isEditMode() {
    return !!this.user();
  }

  hasAvatarImage() {
    const avatar = this.avatarPreview();
    return avatar.startsWith('data:image/') || avatar.startsWith('http://') || avatar.startsWith('https://');
  }

  onAvatarSelect(event: FileSelectEvent) {
    const file = event.files?.[0];

    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const result = typeof reader.result === 'string' ? reader.result : '';
      this.form.controls.avatar.setValue(result);
      this.avatarPreview.set(result);
    };
    reader.readAsDataURL(file);
  }

  hasPasswordRuleError() {
    const control = this.form.controls.password;
    return control.touched && control.hasError('pattern');
  }

  onHide() {
    this.visible.set(false);
  }

  save() {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const model = this.form.getRawValue();
    if (this.isEditMode()) {
      this.saved.emit({
        email: model.email.trim(),
        displayName: model.displayName.trim() || undefined,
        avatar: model.avatar.trim() || undefined,
        isActive: model.isActive,
        isEmailVerified: model.isEmailVerified,
        roles: model.roles
      });
      return;
    }

    this.saved.emit({
      username: model.username.trim(),
      email: model.email.trim(),
      displayName: model.displayName.trim() || undefined,
      avatar: model.avatar.trim() || undefined,
      password: model.password,
      isActive: model.isActive,
      isEmailVerified: model.isEmailVerified,
      roles: model.roles
    });
  }

  protected readonly maxAvatarSize = MAX_AVATAR_SIZE;
}
