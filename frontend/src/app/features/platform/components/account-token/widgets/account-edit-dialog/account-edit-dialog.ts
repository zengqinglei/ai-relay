import { CommonModule } from '@angular/common';
import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges,
  inject,
  signal
} from '@angular/core';
import { FormBuilder, FormGroup, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { PanelModule } from 'primeng/panel';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs/operators';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import {
  AUTH_METHOD_OPTIONS,
  PROVIDER_OPTIONS,
  RATE_LIMIT_SCOPE_OPTIONS
} from '../../../../../../shared/constants/provider.constants';
import { AuthMethod } from '../../../../../../shared/models/auth-method.enum';
import { Provider } from '../../../../../../shared/models/provider.enum';
import {
  AccountTokenOutputDto,
  CreateAccountTokenInputDto,
  RateLimitScope,
  UpdateAccountTokenInputDto
} from '../../../../models/account-token.dto';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import { AccountTokenService } from '../../../../services/account-token-service';
import { ProviderGroupService } from '../../../../services/provider-group-service';

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
    InputNumberModule,
    AutoCompleteModule,
    PanelModule,
    SelectButtonModule,
    ToggleSwitchModule,
    TagModule
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
  providerGroupService = inject(ProviderGroupService);
  cdr = inject(ChangeDetectorRef);

  form: FormGroup;
  isEditMode = signal(false);
  currentProvider = signal<Provider>(Provider.Gemini);
  currentAuthMethod = signal<AuthMethod>(AuthMethod.OAuth);

  authCodeInput = '';
  generatedAuthUrl = '';
  sessionId = '';
  generatingUrl = false;
  authCodeTouched = false;

  modelWhites: string[] = [];
  filteredModels: string[] = [];
  availableModels: string[] = [];
  modelMappings: Array<{ from: string; to: string }> = [];

  allProviderGroups = signal<ProviderGroupOutputDto[]>([]);
  filteredProviderGroups = signal<ProviderGroupOutputDto[]>([]);
  selectedProviderGroups = signal<ProviderGroupOutputDto[]>([]);

  providerOptions = PROVIDER_OPTIONS;
  authMethodOptions = AUTH_METHOD_OPTIONS;
  rateLimitScopeOptions = RATE_LIMIT_SCOPE_OPTIONS;
  dialogConfig = DIALOG_CONFIGS.SMALL;

  constructor() {
    this.form = this.fb.group({
      name: ['', [Validators.required, Validators.minLength(1), Validators.maxLength(256)]],
      provider: [Provider.Gemini, Validators.required],
      authMethod: [AuthMethod.OAuth, Validators.required],
      projectId: [''],
      baseUrl: ['', [Validators.maxLength(512), Validators.pattern(/^(?:https?:\/\/.+|)$/)]],
      credential: ['', [Validators.required, Validators.maxLength(2048)]],
      description: ['', Validators.maxLength(1000)],
      maxConcurrency: [10, [Validators.required, Validators.min(0), Validators.max(1000)]],
      priority: [1, [Validators.required, Validators.min(1), Validators.max(1000)]],
      weight: [50, [Validators.required, Validators.min(1), Validators.max(100)]],
      rateLimitScope: [RateLimitScope.Account, Validators.required],
      allowOfficialClientMimic: [false],
      isCheckStreamHealth: [false]
    });

    this.form.get('provider')?.valueChanges.subscribe(val => {
      if (val) {
        this.currentProvider.set(val);
        this.updateValidators();
        if (!this.isEditMode()) {
          this.loadAvailableModels(val as Provider);
          this.form.get('allowOfficialClientMimic')?.setValue(this.currentAuthMethod() === AuthMethod.OAuth, { emitEvent: false });
        }
        if (val === Provider.Antigravity) {
          this.form.get('authMethod')?.setValue(AuthMethod.OAuth);
          this.form.get('authMethod')?.disable({ emitEvent: false });

          this.form.get('allowOfficialClientMimic')?.setValue(true, { emitEvent: false });
          this.form.get('allowOfficialClientMimic')?.disable({ emitEvent: false });
        } else if (val === Provider.OpenAICompatible) {
          this.form.get('authMethod')?.setValue(AuthMethod.ApiKey);
          this.form.get('authMethod')?.disable({ emitEvent: false });

          this.form.get('allowOfficialClientMimic')?.enable({ emitEvent: false });
        } else {
          if (!this.isEditMode()) {
            this.form.get('authMethod')?.enable({ emitEvent: false });
          }
          this.form.get('allowOfficialClientMimic')?.enable({ emitEvent: false });
        }
      }

      this.authCodeInput = '';
      this.generatedAuthUrl = '';
      this.sessionId = '';
    });

    this.form.get('authMethod')?.valueChanges.subscribe(val => {
      if (val) {
        this.currentAuthMethod.set(val);
        this.updateValidators();
        if (!this.isEditMode()) {
          this.form.get('allowOfficialClientMimic')?.setValue(val === AuthMethod.OAuth, { emitEvent: false });
        }
      }

      this.authCodeInput = '';
      this.generatedAuthUrl = '';
      this.sessionId = '';
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    let shouldInit = false;

    if (changes['account'] && this.visible) {
      shouldInit = true;
    }
    if (changes['visible'] && this.visible) {
      shouldInit = true;
      this.authCodeInput = '';
      this.generatedAuthUrl = '';
      this.sessionId = '';
      this.authCodeTouched = false;
      this.loadProviderGroups();
    }

    if (shouldInit) {
      this.initForm();
    }
  }

  private loadProviderGroups() {
    this.providerGroupService.getAll().subscribe(groups => {
      this.allProviderGroups.set(groups);
      this.syncSelectedProviderGroups();
      this.cdr.markForCheck();
    });
  }

  initForm() {
    if (this.account) {
      this.isEditMode.set(true);
      const projectId = this.account.extraProperties?.['project_id'] || '';

      this.form.patchValue(
        {
          name: this.account.name,
          provider: this.account.provider,
          authMethod: this.account.authMethod,
          projectId,
          baseUrl: this.account.baseUrl,
          credential: '',
          description: this.account.description,
          maxConcurrency: this.account.maxConcurrency,
          priority: this.account.priority,
          weight: this.account.weight,
          rateLimitScope: this.account.rateLimitScope ?? RateLimitScope.Account,
          allowOfficialClientMimic: this.account.allowOfficialClientMimic ?? false,
          isCheckStreamHealth: this.account.isCheckStreamHealth ?? false
        },
        { emitEvent: false }
      );
      this.currentProvider.set(this.account.provider);
      this.currentAuthMethod.set(this.account.authMethod);
      this.form.get('name')?.disable({ emitEvent: false });
      this.form.get('provider')?.disable({ emitEvent: false });
      this.form.get('authMethod')?.disable({ emitEvent: false });

      this.modelWhites = this.account.modelWhites ? [...this.account.modelWhites] : [];
      this.filteredModels = [];
      this.modelMappings =
        this.account.modelMapping && Object.keys(this.account.modelMapping).length > 0
          ? Object.entries(this.account.modelMapping).map(([from, to]) => ({ from, to }))
          : [{ from: '', to: '' }];

      this.syncSelectedProviderGroups();
      this.updateValidators();
      this.loadAvailableModels(this.account.provider, this.account.id);

      if (this.account.provider === Provider.Antigravity) {
        this.form.get('allowOfficialClientMimic')?.setValue(true, { emitEvent: false });
        this.form.get('allowOfficialClientMimic')?.disable({ emitEvent: false });
      } else {
        this.form.get('allowOfficialClientMimic')?.enable({ emitEvent: false });
      }
    } else {
      this.isEditMode.set(false);
      this.form.reset(
        {
          provider: Provider.Gemini,
          authMethod: AuthMethod.OAuth,
          credential: '',
          maxConcurrency: 10,
          priority: 1,
          weight: 50,
          rateLimitScope: RateLimitScope.Account,
          allowOfficialClientMimic: true,
          isCheckStreamHealth: false
        },
        { emitEvent: false }
      );
      this.currentProvider.set(Provider.Gemini);
      this.currentAuthMethod.set(AuthMethod.OAuth);
      this.form.get('name')?.enable({ emitEvent: false });
      this.form.get('provider')?.enable({ emitEvent: false });
      this.form.get('authMethod')?.enable({ emitEvent: false });

      this.modelWhites = [];
      this.filteredModels = [];
      this.modelMappings = [{ from: '', to: '' }];

      this.selectedProviderGroups.set([]);
      this.ensureDefaultGroupSelected();
      this.updateValidators();
      this.loadAvailableModels(Provider.Gemini);
    }

    this.cdr.markForCheck();
  }

  private syncSelectedProviderGroups() {
    const groups = this.allProviderGroups();
    if (!groups.length) {
      return;
    }

    if (this.account?.providerGroupIds?.length) {
      const selected = groups.filter(group => this.account?.providerGroupIds.includes(group.id));
      this.selectedProviderGroups.set(selected.length ? selected : this.getDefaultGroupSelection(groups));
      return;
    }

    if (!this.isEditMode()) {
      this.ensureDefaultGroupSelected();
    }
  }

  private ensureDefaultGroupSelected() {
    if (this.selectedProviderGroups().length) {
      return;
    }

    const defaults = this.getDefaultGroupSelection(this.allProviderGroups());
    if (defaults.length) {
      this.selectedProviderGroups.set(defaults);
    }
  }

  private getDefaultGroupSelection(groups: ProviderGroupOutputDto[]): ProviderGroupOutputDto[] {
    const defaultGroup = groups.find(group => group.isDefault);
    return defaultGroup ? [defaultGroup] : [];
  }

  searchProviderGroups(event: { query: string }) {
    const query = event.query.trim().toLowerCase();
    const selectedIds = new Set(this.selectedProviderGroups().map(group => group.id));

    this.filteredProviderGroups.set(
      this.allProviderGroups().filter(group => {
        if (selectedIds.has(group.id)) {
          return false;
        }

        if (!query) {
          return true;
        }

        return group.name.toLowerCase().includes(query) || (group.description ?? '').toLowerCase().includes(query);
      })
    );
  }

  onProviderGroupsChange(groups: ProviderGroupOutputDto[]) {
    this.selectedProviderGroups.set(groups ?? []);
    this.cdr.markForCheck();
  }

  get providerGroupsInvalid(): boolean {
    return this.selectedProviderGroups().length === 0;
  }

  updateValidators() {
    const isOAuth = this.currentAuthMethod() === AuthMethod.OAuth;

    if (isOAuth) {
      this.form.get('credential')?.clearValidators();
    } else if (this.isEditMode()) {
      this.form.get('credential')?.setValidators([Validators.maxLength(2048)]);
    } else {
      this.form.get('credential')?.setValidators([Validators.required, Validators.maxLength(2048)]);
    }
    this.form.get('credential')?.updateValueAndValidity();

    const baseUrlControl = this.form.get('baseUrl');
    if (this.currentProvider() === Provider.OpenAICompatible) {
      baseUrlControl?.setValidators([Validators.required, Validators.maxLength(512), Validators.pattern(/^(?:https?:\/\/.+|)$/)]);
    } else {
      baseUrlControl?.setValidators([Validators.maxLength(512), Validators.pattern(/^(?:https?:\/\/.+|)$/)]);
    }
    baseUrlControl?.updateValueAndValidity();
  }

  get isOAuth(): boolean {
    return this.currentAuthMethod() === AuthMethod.OAuth;
  }

  get isApiKeyMode(): boolean {
    return this.currentAuthMethod() === AuthMethod.ApiKey;
  }

  get showOAuthFlow(): boolean {
    return this.currentAuthMethod() === AuthMethod.OAuth;
  }

  get showOfficialMimic(): boolean {
    return true;
  }

  get showProjectId(): boolean {
    return this.currentProvider() === Provider.Gemini && this.currentAuthMethod() === AuthMethod.OAuth;
  }

  onHide() {
    this.visibleChange.emit(false);
  }

  onSubmit() {
    if (this.showOAuthFlow && !this.isEditMode() && !this.authCodeInput) {
      this.authCodeTouched = true;
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      this.cdr.markForCheck();
      return;
    }

    if (this.providerGroupsInvalid) {
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      this.cdr.markForCheck();
      return;
    }

    if (this.form.valid) {
      if (this.hasMappingError) {
        this.cdr.markForCheck();
        return;
      }

      const formValue = this.form.getRawValue();
      const extraProperties: Record<string, string> = {};
      if (this.isEditMode() && this.account?.extraProperties) {
        Object.assign(extraProperties, this.account.extraProperties);
      }

      if (formValue.projectId) {
        extraProperties['project_id'] = formValue.projectId;
      } else if (extraProperties['project_id']) {
        delete extraProperties['project_id'];
      }

      const providerGroupIds = this.selectedProviderGroups().map(group => group.id);

      const createDto: CreateAccountTokenInputDto = {
        name: formValue.name,
        provider: formValue.provider,
        authMethod: formValue.authMethod,
        extraProperties: Object.keys(extraProperties).length > 0 ? extraProperties : undefined,
        baseUrl: formValue.baseUrl,
        description: formValue.description,
        maxConcurrency: formValue.maxConcurrency,
        priority: formValue.priority,
        weight: formValue.weight,
        providerGroupIds,
        modelWhites: this.modelWhites,
        modelMapping: this.buildModelMapping(),
        rateLimitScope: formValue.rateLimitScope ?? RateLimitScope.Account,
        allowOfficialClientMimic: formValue.allowOfficialClientMimic ?? false,
        isCheckStreamHealth: formValue.isCheckStreamHealth ?? false
      };

      if (this.showOAuthFlow) {
        createDto.authCode = this.authCodeInput;
        createDto.sessionId = this.sessionId;
      } else {
        createDto.credential = formValue.credential;
      }

      if (this.isEditMode() && this.account) {
        const updateDto: UpdateAccountTokenInputDto = {
          name: createDto.name,
          extraProperties: createDto.extraProperties,
          baseUrl: createDto.baseUrl,
          description: createDto.description,
          maxConcurrency: createDto.maxConcurrency,
          priority: createDto.priority,
          weight: createDto.weight,
          providerGroupIds: createDto.providerGroupIds,
          modelWhites: createDto.modelWhites,
          modelMapping: createDto.modelMapping,
          rateLimitScope: createDto.rateLimitScope,
          allowOfficialClientMimic: createDto.allowOfficialClientMimic,
          isCheckStreamHealth: createDto.isCheckStreamHealth
        };

        if (createDto.credential) {
          updateDto.credential = createDto.credential;
        }

        this.save.emit({ id: this.account.id, dto: updateDto });
      } else {
        this.save.emit({ dto: createDto });
      }
    } else {
      Object.keys(this.form.controls).forEach(key => {
        this.form.get(key)?.markAsTouched();
      });
      this.cdr.markForCheck();
    }
  }

  generateAuthUrl() {
    const provider = this.currentProvider();
    this.generatingUrl = true;
    this.cdr.markForCheck();

    this.accountService
      .getAuthUrl(provider)
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

    this.authCodeTouched = false;

    const code = this.authCodeInput.trim();
    if (code.includes('http') || code.includes('code=')) {
      try {
        const url = new URL(code);
        const codeParam = url.searchParams.get('code');
        if (codeParam) {
          this.authCodeInput = decodeURIComponent(codeParam);
        }
      } catch {
        const match = code.match(/code=([^&]+)/);
        if (match) {
          this.authCodeInput = decodeURIComponent(match[1]);
        }
      }
    }
  }

  loadAvailableModels(provider: Provider, accountId?: string) {
    this.availableModels = [];
    this.accountService.getAvailableModels(provider, accountId).subscribe({
      next: models => {
        this.availableModels = models.map(m => m.value);
        this.cdr.markForCheck();
      },
      error: () => {
        this.availableModels = [];
      }
    });
  }

  filterModels(event: { query: string }) {
    const originalQuery = event.query.trim();
    const lowerQuery = originalQuery.toLowerCase();
    const existing = new Set(this.modelWhites);
    const matched = this.availableModels.filter(m => !existing.has(m) && (lowerQuery === '' || m.toLowerCase().includes(lowerQuery)));
    this.filteredModels =
      originalQuery && !this.availableModels.some(m => m.toLowerCase() === lowerQuery) ? [originalQuery, ...matched] : matched;
  }

  onWhitelistSelect(event: { value: string }) {
    const val = event.value?.trim();
    if (val && !this.modelWhites.includes(val)) {
      this.modelWhites = [...this.modelWhites, val];
    }
  }

  onWhitelistUnselect(event: { value: string }) {
    this.modelWhites = this.modelWhites.filter(m => m !== event.value);
  }

  addMappingRow() {
    this.modelMappings = [...this.modelMappings, { from: '', to: '' }];
  }

  removeMappingRow(index: number) {
    if (this.modelMappings.length > 1) {
      this.modelMappings = this.modelMappings.filter((_, i) => i !== index);
    } else {
      this.modelMappings = [{ from: '', to: '' }];
    }
  }

  get hasMappingError(): boolean {
    if (this.modelMappings.length <= 1) {
      const m = this.modelMappings[0] ?? { from: '', to: '' };
      const hasFrom = m.from.trim() !== '';
      const hasTo = m.to.trim() !== '';
      return hasFrom !== hasTo;
    }

    return this.modelMappings.some(m => m.from.trim() === '' || m.to.trim() === '');
  }

  private buildModelMapping(): Record<string, string> | undefined {
    const entries = this.modelMappings.filter(m => m.from.trim() && m.to.trim());
    if (entries.length > 0) {
      return Object.fromEntries(entries.map(m => [m.from.trim(), m.to.trim()]));
    }
    if (this.isEditMode() && this.account?.modelMapping && Object.keys(this.account.modelMapping).length > 0) {
      return {};
    }
    return undefined;
  }

  trackByIndex(index: number) {
    return index;
  }
}
