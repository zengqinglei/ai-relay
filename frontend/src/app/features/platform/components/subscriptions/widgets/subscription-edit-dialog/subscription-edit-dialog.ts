import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { PROVIDER_PLATFORM_OPTIONS } from '../../../../../../shared/constants/provider-platform.constants';
import { ProviderPlatform } from '../../../../../../shared/models/provider-platform.enum';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import { ApiKeyOutputDto, ApiKeyBindGroupInputDto } from '../../../../models/subscription.dto';
import { ProviderGroupService } from '../../../../services/provider-group-service';

@Component({
  selector: 'app-subscription-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    DatePickerModule,
    SelectModule,
    DividerModule,
    ProgressSpinnerModule,
    TooltipModule,
    PlatformIcon
  ],
  templateUrl: './subscription-edit-dialog.html'
})
export class SubscriptionEditDialogComponent {
  @Input() visible = false;
  @Input() loading = false; // Loading for fetching subscription details
  @Input() saving = false; // Loading for saving operation
  @Input() set subscription(value: ApiKeyOutputDto | null) {
    if (value) {
      this.isEditMode.set(true);
      this.formModel.set({
        name: value.name,
        description: value.description,
        expiresAt: value.expiresAt,
        customSecret: '', // Not editable in edit mode
        bindings: [] // Will be handled via map
      });
      // Parse Date
      this.expiryDate = value.expiresAt ? new Date(value.expiresAt) : null;
      // Parse Bindings
      this.initializeBindingsMap(value.bindings);
    } else {
      this.isEditMode.set(false);
      this.formModel.set(this.createEmptyModel());
      this.expiryDate = null;
      this.bindingsMap = {};
    }
  }
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<any>(); // CreateApiKeyInputDto | UpdateApiKeyInputDto

  private groupService = inject(ProviderGroupService);

  isEditMode = signal(false);
  formModel = signal<any>(this.createEmptyModel());

  expiryDate: Date | null = null;

  // Platform -> GroupId
  bindingsMap: { [key: string]: string } = {};

  // Cache for groups options
  allGroups = signal<ProviderGroupOutputDto[]>([]);

  platformOptions = PROVIDER_PLATFORM_OPTIONS;

  // 使用小型 Dialog 配置
  dialogConfig = DIALOG_CONFIGS.SMALL;

  constructor() {
    // Load groups on init (simple strategy)
    this.groupService.getGroups({ offset: 0, limit: 1000 }).subscribe(result => this.allGroups.set(result.items));
  }

  createEmptyModel() {
    return {
      name: '',
      description: '',
      expiresAt: null,
      customSecret: '',
      bindings: []
    };
  }

  initializeBindingsMap(bindings: any[]) {
    this.bindingsMap = {};
    if (bindings) {
      bindings.forEach(b => {
        this.bindingsMap[b.platform] = b.providerGroupId;
      });
    }
  }

  getGroupsForPlatform(platform: string) {
    return this.allGroups().filter(g => g.platform === platform);
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.formModel.set(this.createEmptyModel());
    this.expiryDate = null;
    this.bindingsMap = {};
  }

  isValid(): boolean {
    if (!this.formModel().name) return false;

    // If customSecret is provided, validate it
    if (this.formModel().customSecret && !this.isSecretValid()) {
      return false;
    }

    return true;
  }

  isSecretValid(): boolean {
    const secret = this.formModel().customSecret;
    if (!secret) return true; // Empty is valid (will auto-generate)

    // Regex: 6-48 characters, must contain numbers and letters, can contain underscore or hyphen
    const regex = /^(?=.*[0-9])(?=.*[a-zA-Z])[a-zA-Z0-9_-]{6,48}$/;
    return regex.test(secret);
  }

  save() {
    if (this.isValid()) {
      // Convert Map to Array
      const bindings: ApiKeyBindGroupInputDto[] = Object.keys(this.bindingsMap)
        .filter(key => !!this.bindingsMap[key])
        .map(key => ({
          platform: key as ProviderPlatform,
          providerGroupId: this.bindingsMap[key]
        }));

      const payload = {
        ...this.formModel(),
        expiresAt: this.expiryDate ? this.expiryDate.toISOString() : null,
        bindings: bindings
      };

      this.saved.emit(payload);
    }
  }
}
