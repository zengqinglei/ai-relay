import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  EventEmitter,
  Input,
  Output,
  signal,
  OnChanges,
  SimpleChanges,
  inject,
  ChangeDetectorRef
} from '@angular/core';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs/operators';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { PROVIDER_PLATFORM_OPTIONS } from '../../../../../../shared/constants/provider-platform.constants';
import { ProviderPlatform } from '../../../../../../shared/models/provider-platform.enum';
import { AccountTokenOutputDto, CreateAccountTokenInputDto, UpdateAccountTokenInputDto } from '../../../../models/account-token.dto';
import { AccountTokenService } from '../../../../services/account-token-service';

@Component({
  selector: 'app-account-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    TooltipModule,
    InputNumberModule
  ],
  templateUrl: './account-edit-dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class AccountEditDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() account: AccountTokenOutputDto | null = null;
  @Input() saving = false;
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly save = new EventEmitter<{ id?: string; dto: CreateAccountTokenInputDto | UpdateAccountTokenInputDto }>();

  fb = inject(FormBuilder);
  accountService = inject(AccountTokenService);
  cdr = inject(ChangeDetectorRef);

  form: FormGroup;
  isEditMode = signal(false);
  platformType = signal<string>(ProviderPlatform.GEMINI_OAUTH);

  // OAuth State
  authCodeInput = '';
  generatedAuthUrl = '';
  sessionId = '';
  generatingUrl = false;
  authCodeTouched = false;

  platformOptions = PROVIDER_PLATFORM_OPTIONS;

  // 使用小型 Dialog 配置
  dialogConfig = DIALOG_CONFIGS.SMALL;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(1), Validators.maxLength(256)]],
      platform: [ProviderPlatform.GEMINI_OAUTH, Validators.required],
      projectId: [''],
      baseUrl: ['', [Validators.maxLength(512), Validators.pattern(/^https?:\/\/.+/)]],
      credential: ['', [Validators.required, Validators.maxLength(2048)]],
      description: ['', Validators.maxLength(1000)],
      maxConcurrency: [10, [Validators.required, Validators.min(0), Validators.max(1000)]]
    });

    this.form.get('platform')?.valueChanges.subscribe(val => {
      if (val) {
        this.platformType.set(val);
        this.updateValidators(val);
      }
      // Clear OAuth state when platform changes
      this.authCodeInput = '';
      this.generatedAuthUrl = '';
      this.sessionId = '';
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['account'] && this.visible) {
      this.initForm();
    }
    if (changes['visible'] && this.visible) {
      this.initForm();
      // Reset OAuth state on open
      this.authCodeInput = '';
      this.generatedAuthUrl = '';
      this.sessionId = '';
      this.authCodeTouched = false;
    }
  }

  initForm() {
    if (this.account) {
      this.isEditMode.set(true);
      const projectId = this.account.extraProperties?.['project_id'] || '';

      this.form.patchValue({
        name: this.account.name,
        platform: this.account.platform,
        projectId: projectId,
        baseUrl: this.account.baseUrl,
        credential: '', // Don't fill credential
        description: this.account.description,
        maxConcurrency: this.account.maxConcurrency
      });
      this.platformType.set(this.account.platform);
      this.form.get('name')?.disable();
      this.form.get('platform')?.disable();

      this.updateValidators(this.account.platform);
    } else {
      this.isEditMode.set(false);
      this.form.reset({
        platform: ProviderPlatform.GEMINI_OAUTH,
        credential: '',
        maxConcurrency: 10
      });
      this.platformType.set(ProviderPlatform.GEMINI_OAUTH);
      this.form.get('name')?.enable();
      this.form.get('platform')?.enable();

      this.updateValidators(ProviderPlatform.GEMINI_OAUTH);
    }
  }

  updateValidators(platform: string) {
    const isOAuth = this.isOAuthPlatform(platform);

    if (isOAuth) {
      this.form.get('credential')?.clearValidators();
    } else {
      // API Key 类型：编辑模式下允许为空（后端不修改），创建模式下必填
      if (this.isEditMode()) {
        this.form.get('credential')?.setValidators([Validators.maxLength(2048)]);
      } else {
        this.form.get('credential')?.setValidators([Validators.required, Validators.maxLength(2048)]);
      }
    }
    this.form.get('credential')?.updateValueAndValidity();
  }

  isOAuthPlatform(platform: string): boolean {
    return (
      platform === ProviderPlatform.GEMINI_OAUTH ||
      platform === ProviderPlatform.ANTIGRAVITY ||
      platform === ProviderPlatform.CLAUDE_OAUTH ||
      platform === ProviderPlatform.OPENAI_OAUTH
    );
  }

  onHide() {
    this.visibleChange.emit(false);
  }

  onSubmit() {
    // If OAuth, ensure authCode is present if we are creating, or if we are editing and want to update token
    if (this.showOAuthFlow && !this.isEditMode() && !this.authCodeInput) {
      // Manual validation for Auth Code in Create mode
      this.authCodeTouched = true;
      // Mark form as touched to show other validation errors
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      this.cdr.markForCheck();
      return;
    }

    if (this.form.valid) {
      const formValue = this.form.getRawValue();

      // Construct extraProperties
      const extraProperties: Record<string, string> = {};
      if (this.isEditMode() && this.account?.extraProperties) {
        Object.assign(extraProperties, this.account.extraProperties);
      }

      if (formValue.projectId) {
        extraProperties['project_id'] = formValue.projectId;
      } else if (extraProperties['project_id']) {
        // If cleared in form, remove from extraProperties
        delete extraProperties['project_id'];
      }

      const createDto: CreateAccountTokenInputDto = {
        name: formValue.name,
        platform: formValue.platform,
        extraProperties: Object.keys(extraProperties).length > 0 ? extraProperties : undefined,
        baseUrl: formValue.baseUrl,
        description: formValue.description,
        maxConcurrency: formValue.maxConcurrency
      };

      if (this.showOAuthFlow) {
        // OAuth Mode: Pass authCode and sessionId
        createDto.authCode = this.authCodeInput;
        createDto.sessionId = this.sessionId;
        // Backend handles exchange, credential field is ignored or empty
      } else {
        // API Key Mode: Pass credential
        createDto.credential = formValue.credential;
      }

      if (this.isEditMode() && this.account) {
        // For update, construct UpdateAccountTokenInputDto (platform is not updatable)
        const updateDto: UpdateAccountTokenInputDto = {
          name: createDto.name,
          extraProperties: createDto.extraProperties,
          baseUrl: createDto.baseUrl,
          description: createDto.description,
          maxConcurrency: createDto.maxConcurrency
        };

        // Only send credential if provided
        if (createDto.credential) {
          updateDto.credential = createDto.credential;
        }

        this.save.emit({ id: this.account.id, dto: updateDto });
      } else {
        this.save.emit({ dto: createDto });
      }
    } else {
      // Mark all fields as touched to trigger validation messages
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      this.cdr.markForCheck();
    }
  }

  get isApiKeyPlatform(): boolean {
    return this.platformType().includes('APIKEY');
  }

  get showOAuthFlow(): boolean {
    return this.isOAuthPlatform(this.platformType());
  }

  get showProjectId(): boolean {
    // Only show Project ID for Gemini Account (OAuth)
    return this.platformType() === ProviderPlatform.GEMINI_OAUTH;
  }

  generateAuthUrl() {
    const platform = this.platformType() as ProviderPlatform;
    this.generatingUrl = true;
    this.cdr.markForCheck();

    this.accountService
      .getAuthUrl(platform)
      .pipe(
        finalize(() => {
          this.generatingUrl = false;
          this.cdr.markForCheck();
        })
      )
      .subscribe({
        next: res => {
          if (res.authUrl) {
            this.generatedAuthUrl = res.authUrl;
            this.sessionId = res.sessionId;
          }
        },
        error: err => {
          console.error('Failed to generate auth url', err);
        }
      });
  }

  copyAuthUrl() {
    if (this.generatedAuthUrl) {
      navigator.clipboard.writeText(this.generatedAuthUrl);
    }
  }

  openAuthUrl() {
    if (this.generatedAuthUrl) {
      window.open(this.generatedAuthUrl, '_blank');
    }
  }

  extractCodeFromUrl() {
    if (!this.authCodeInput) return;

    // Reset touched state when user starts typing
    this.authCodeTouched = false;

    const code = this.authCodeInput.trim();
    // Simple check if it looks like a URL
    if (code.includes('http') || code.includes('code=')) {
      try {
        // Try constructing URL object (might fail if partial)
        const url = new URL(code);
        const codeParam = url.searchParams.get('code');
        if (codeParam) {
          this.authCodeInput = decodeURIComponent(codeParam);
        }
      } catch (e) {
        // Fallback regex
        const match = code.match(/code=([^&]+)/);
        if (match) {
          this.authCodeInput = decodeURIComponent(match[1]);
        }
      }
    }
  }
}
