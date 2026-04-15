import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, effect, inject, model, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { AvatarModule } from 'primeng/avatar';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { FileSelectEvent, FileUploadModule } from 'primeng/fileupload';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { finalize } from 'rxjs/operators';

import { AuthService } from '../../../../core/services/auth-service';
import { DIALOG_CONFIGS } from '../../../../shared/constants/dialog-config.constants';
import { AccountService } from '../../services/account-service';

const MAX_AVATAR_SIZE = 1024 * 1024;
const PHONE_PATTERN = /^[0-9+\-()\s]{0,20}$/;

@Component({
  selector: 'app-profile-settings-dialog',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, DialogModule, ButtonModule, AvatarModule, FileUploadModule, InputTextModule, TagModule],
  templateUrl: './profile-settings-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ProfileSettingsDialogComponent {
  readonly visible = model(false);

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly accountService = inject(AccountService);
  private readonly messageService = inject(MessageService);

  readonly dialogConfig = DIALOG_CONFIGS.SMALL;
  readonly saving = signal(false);
  readonly user = computed(() => this.authService.currentUser());
  readonly avatarPreview = signal('');
  readonly displayName = computed(
    () => this.form.controls.nickname.value.trim() || this.user()?.nickname || this.user()?.username || '未登录用户'
  );
  readonly avatarLabel = computed(() => {
    const text = this.displayName().trim();
    return (text.charAt(0) || 'U').toUpperCase();
  });
  readonly avatarStyle = computed(() => {
    const seed = (this.form.controls.username.value || this.user()?.username || this.displayName()).trim();
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
    nickname: ['', [Validators.maxLength(128)]],
    phoneNumber: ['', [Validators.maxLength(20), Validators.pattern(PHONE_PATTERN)]],
    avatar: ['']
  });

  constructor() {
    effect(() => {
      if (this.visible()) {
        const user = this.user();
        const avatar = user?.avatar ?? '';

        this.form.reset({
          username: user?.username ?? '',
          email: user?.email ?? '',
          nickname: user?.nickname ?? '',
          phoneNumber: user?.phoneNumber ?? '',
          avatar
        });

        this.avatarPreview.set(avatar);
      }
    });
  }

  hasAvatarImage(): boolean {
    const avatar = this.avatarPreview();
    return avatar.startsWith('data:image/') || avatar.startsWith('http://') || avatar.startsWith('https://');
  }

  onAvatarSelect(event: FileSelectEvent): void {
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

  onHide(): void {
    this.visible.set(false);
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const { username, email, nickname, phoneNumber, avatar } = this.form.getRawValue();

    this.accountService
      .updateCurrentUser({
        username: username.trim(),
        email: email.trim(),
        nickname: nickname.trim() || undefined,
        phoneNumber: phoneNumber.trim() || undefined,
        avatar: avatar.trim() || undefined
      })
      .pipe(finalize(() => this.saving.set(false)))
      .subscribe({
        next: () => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '个人资料已更新' });
          this.visible.set(false);
        }
      });
  }

  protected readonly maxAvatarSize = MAX_AVATAR_SIZE;
}
