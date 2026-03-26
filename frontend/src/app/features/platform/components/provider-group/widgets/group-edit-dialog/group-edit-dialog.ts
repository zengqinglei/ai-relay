import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output, signal, inject, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { ProgressSpinnerModule } from 'primeng/progressspinner';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';

import { DIALOG_CONFIGS } from '../../../../../../shared/constants/dialog-config.constants';
import { PROVIDER_PLATFORM_OPTIONS } from '../../../../../../shared/constants/provider-platform.constants';
import { ProviderPlatform } from '../../../../../../shared/models/provider-platform.enum';
import { AccountTokenOutputDto } from '../../../../models/account-token.dto';
import {
  CreateProviderGroupInputDto,
  UpdateProviderGroupInputDto,
  ProviderGroupOutputDto,
  GroupSchedulingStrategy,
  SCHEDULING_STRATEGY_DESCRIPTIONS
} from '../../../../models/provider-group.dto';
import { AccountTokenService } from '../../../../services/account-token-service';

@Component({
  selector: 'app-group-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    DialogModule,
    ButtonModule,
    InputTextModule,
    InputNumberModule,
    TextareaModule,
    SelectModule,
    MultiSelectModule,
    ToggleSwitchModule,
    TableModule,
    DividerModule,
    ProgressSpinnerModule,
    TagModule,
    TooltipModule
  ],
  templateUrl: './group-edit-dialog.html'
})
export class GroupEditDialogComponent {
  @Input() visible = false;
  @Input() loading = false; // Loading for fetching group details
  @Input() saving = false; // Loading for saving operation
  @Input() readonly = false; // Permission: read-only mode disables all controls
  @Input() set group(value: ProviderGroupOutputDto | null) {
    if (value) {
      this.isEditMode.set(true);
      this.formModel.set(JSON.parse(JSON.stringify(value))); // Deep copy
      // Load accounts for this platform
      this.loadAvailableAccounts(value.platform);
    } else {
      this.isEditMode.set(false);
      this.formModel.set(this.createEmptyGroup());
    }
  }
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<CreateProviderGroupInputDto | UpdateProviderGroupInputDto>();

  private accountService = inject(AccountTokenService);

  isEditMode = signal(false);
  formModel = signal<any>(this.createEmptyGroup()); // strict type tricky with different input/output, using any for form model temporarily or better mapping

  // Available accounts for selection (filtered by platform)
  availableAccounts = signal<AccountTokenOutputDto[]>([]);
  selectedAccountsToAdd: string[] = [];

  // Accounts not yet added to the group (excludes already-added ones)
  unaddedAccounts = computed(() =>
    this.availableAccounts().filter(a => !this.isAccountAdded(a.id))
  );

  platformOptions = PROVIDER_PLATFORM_OPTIONS;

  // 使用中型 Dialog 配置（因为包含复杂表格）
  dialogConfig = DIALOG_CONFIGS.MEDIUM;

  /**
   * 获取可用的调度策略选项（根据平台动态显示）
   */
  strategyOptions = computed(() => {
    const platform = this.formModel().platform;

    const baseStrategies = [
      { label: '自适应均衡（推荐）', value: GroupSchedulingStrategy.AdaptiveBalanced },
      { label: '加权随机', value: GroupSchedulingStrategy.WeightedRandom },
      { label: '优先级降级', value: GroupSchedulingStrategy.Priority }
    ];

    // QuotaPriority 仅支持 ANTIGRAVITY 和 GEMINI_OAUTH
    if (platform === ProviderPlatform.ANTIGRAVITY || platform === ProviderPlatform.GEMINI_OAUTH) {
      baseStrategies.push({
        label: '配额优先（智能）',
        value: GroupSchedulingStrategy.QuotaPriority
      });
    }

    return baseStrategies;
  });

  constructor() {}

  // Helpers for UI logic
  get showWeight(): boolean {
    return this.formModel().schedulingStrategy === GroupSchedulingStrategy.WeightedRandom;
  }

  get showPriority(): boolean {
    return this.formModel().schedulingStrategy === GroupSchedulingStrategy.Priority;
  }

  createEmptyGroup(): any {
    return {
      name: '',
      description: '',
      platform: null,
      schedulingStrategy: GroupSchedulingStrategy.AdaptiveBalanced,
      rateMultiplier: 1.0,
      enableStickySession: true,
      stickySessionExpirationHours: 1,
      accounts: []
    };
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);

    // Reset state on close to ensure next open is clean
    // This is crucial because if 'group' input doesn't change (null -> null),
    // the setter won't trigger a reset.
    this.formModel.set(this.createEmptyGroup());
    this.isEditMode.set(false);
    this.selectedAccountsToAdd = [];
    this.availableAccounts.set([]);
  }

  onPlatformChange() {
    // Platform changed, clear accounts and multiselect selection
    this.formModel.update(m => ({ ...m, accounts: [] }));
    this.selectedAccountsToAdd = [];

    // 如果当前策略是 QuotaPriority，但新平台不支持，则重置为默认策略
    const currentStrategy = this.formModel().schedulingStrategy;
    const newPlatform = this.formModel().platform;

    if (
      currentStrategy === GroupSchedulingStrategy.QuotaPriority &&
      newPlatform !== ProviderPlatform.ANTIGRAVITY &&
      newPlatform !== ProviderPlatform.GEMINI_OAUTH
    ) {
      this.formModel.update(m => ({
        ...m,
        schedulingStrategy: GroupSchedulingStrategy.AdaptiveBalanced
      }));
    }

    if (this.formModel().platform) {
      this.loadAvailableAccounts(this.formModel().platform);
    }
  }

  loadAvailableAccounts(platform: ProviderPlatform) {
    // Load ALL accounts for this platform (simple mock approach)
    // In real app might use a specific API to get candidate accounts
    this.accountService.getAccounts({ platform: platform, offset: 0, limit: 1000 }).subscribe(result => {
      this.availableAccounts.set(result.items);
    });
  }

  isAccountAdded(accountId: string): boolean {
    return this.formModel().accounts.some((a: any) => a.accountTokenId === accountId);
  }

  addAccount() {
    if (!this.selectedAccountsToAdd.length) return;

    const newAccounts = this.selectedAccountsToAdd
      .map(id => this.availableAccounts().find(a => a.id === id))
      .filter((acc): acc is AccountTokenOutputDto => !!acc && !this.isAccountAdded(acc.id))
      .map(acc => ({
        accountTokenId: acc.id,
        accountTokenName: acc.name,
        weight: 1,
        priority: 0
      }));

    if (newAccounts.length) {
      this.formModel.update(m => ({ ...m, accounts: [...m.accounts, ...newAccounts] }));
    }
    this.selectedAccountsToAdd = [];
  }

  removeAccount(index: number) {
    const accounts = [...this.formModel().accounts];
    accounts.splice(index, 1);
    this.formModel.update(m => ({ ...m, accounts }));
  }

  getAccountName(id: string): string {
    // Try to find in form model's accountTokenName (snapshot) or available accounts
    const inForm = this.formModel().accounts.find((a: any) => a.accountTokenId === id);
    if (inForm?.accountTokenName) return inForm.accountTokenName;

    const inAvailable = this.availableAccounts().find(a => a.id === id);
    return inAvailable?.name || id;
  }

  isValid(): boolean {
    const m = this.formModel();

    // 验证必填字段
    if (!m.name || !m.platform) {
      return false;
    }

    // 验证名称长度（1-256字符）
    if (m.name.length < 1 || m.name.length > 256) {
      return false;
    }

    // 验证描述长度（≤1000字符）
    if (m.description && m.description.length > 1000) {
      return false;
    }

    // 验证费率倍数（0.01-100）
    if (!m.rateMultiplier || m.rateMultiplier < 0.01 || m.rateMultiplier > 100) {
      return false;
    }

    // 验证粘性会话配置（1-8760小时）
    if (m.enableStickySession) {
      if (!m.stickySessionExpirationHours || m.stickySessionExpirationHours < 1 || m.stickySessionExpirationHours > 8760) {
        return false;
      }
    }

    return true;
  }

  /**
   * 获取调度策略详细说明
   */
  getStrategyDescription(strategy: GroupSchedulingStrategy): string {
    return SCHEDULING_STRATEGY_DESCRIPTIONS[strategy] || '';
  }

  save() {
    if (this.isValid()) {
      this.saved.emit(this.formModel());
    }
  }
}
