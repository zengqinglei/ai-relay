import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, signal, inject, computed } from '@angular/core';
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
import { getProviderAuthLabel } from '../../../../../../shared/constants/provider.constants';
import { ROUTE_PROFILE_LABELS, ROUTE_PROFILE_SUPPORTED_COMBINATIONS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { AccountTokenOutputDto } from '../../../../models/account-token.dto';
import {
  AddGroupAccountInputDto,
  CreateProviderGroupInputDto,
  UpdateProviderGroupInputDto,
  ProviderGroupOutputDto,
  GroupSchedulingStrategy,
  SCHEDULING_STRATEGY_DESCRIPTIONS
} from '../../../../models/provider-group.dto';
import { AccountTokenService } from '../../../../services/account-token-service';

type GroupedAccountOption = {
  label: string;
  routeLabels: string[];
  items: AccountTokenOutputDto[];
};

type GroupSelectionState = 'all' | 'partial' | 'none';

type ProviderGroupEditFormModel = {
  name: string;
  description?: string;
  schedulingStrategy: GroupSchedulingStrategy;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
  accounts: AddGroupAccountInputDto[];
};

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
export class GroupEditDialogComponent implements OnChanges {
  @Input() visible = false;
  @Input() loading = false;
  @Input() saving = false;
  @Input() readonly = false;
  @Input() set group(value: ProviderGroupOutputDto | null) {
    if (value) {
      this.isEditMode.set(true);
      this.formModel.set(this.mapToFormModel(value));
      this.accountDisplayMap.set(this.buildAccountDisplayMap(value));
    } else {
      this.isEditMode.set(false);
      this.formModel.set(this.createEmptyGroup());
      this.accountDisplayMap.set({});
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['visible']?.currentValue === true && changes['visible']?.previousValue !== true) {
      this.loadAvailableAccounts();
    }
  }
  @Output() readonly visibleChange = new EventEmitter<boolean>();
  @Output() readonly saved = new EventEmitter<CreateProviderGroupInputDto | UpdateProviderGroupInputDto>();

  private accountService = inject(AccountTokenService);

  isEditMode = signal(false);
  formModel = signal<ProviderGroupEditFormModel>(this.createEmptyGroup());
  accountDisplayMap = signal<Record<string, { name: string; label: string }>>({});

  availableAccounts = signal<AccountTokenOutputDto[]>([]);
  selectedAccountsToAdd: string[] = [];

  unaddedAccounts = computed(() => this.availableAccounts().filter(a => !this.isAccountAdded(a.id)));

  groupedUnaddedAccounts = computed<GroupedAccountOption[]>(() => {
    const groups = new Map<string, GroupedAccountOption>();

    for (const account of this.unaddedAccounts()) {
      const label = getProviderAuthLabel(account.provider, account.authMethod);
      const routeLabels = this.getSupportedRouteLabels(account);
      const existing = groups.get(label);
      if (existing) {
        existing.items.push(account);
        existing.routeLabels = Array.from(new Set([...existing.routeLabels, ...routeLabels]));
      } else {
        groups.set(label, {
          label,
          routeLabels,
          items: [account]
        });
      }
    }

    return Array.from(groups.values());
  });

  dialogConfig = DIALOG_CONFIGS.MEDIUM;

  strategyOptions = computed(() => {
    return [
      { label: '自适应均衡（推荐）', value: GroupSchedulingStrategy.AdaptiveBalanced },
      { label: '加权随机', value: GroupSchedulingStrategy.WeightedRandom },
      { label: '优先级降级', value: GroupSchedulingStrategy.Priority },
      { label: '配额优先（智能）', value: GroupSchedulingStrategy.QuotaPriority }
    ];
  });

  readonly getProviderAuthLabel = getProviderAuthLabel;

  constructor() {}

  get showWeight(): boolean {
    return this.formModel().schedulingStrategy === GroupSchedulingStrategy.WeightedRandom;
  }

  get showPriority(): boolean {
    return this.formModel().schedulingStrategy === GroupSchedulingStrategy.Priority;
  }

  createEmptyGroup(): ProviderGroupEditFormModel {
    return {
      name: '',
      description: '',
      schedulingStrategy: GroupSchedulingStrategy.AdaptiveBalanced,
      rateMultiplier: 1.0,
      enableStickySession: true,
      stickySessionExpirationHours: 1,
      accounts: []
    };
  }

  private mapToFormModel(group: ProviderGroupOutputDto): ProviderGroupEditFormModel {
    return {
      name: group.name,
      description: group.description,
      schedulingStrategy: group.schedulingStrategy,
      rateMultiplier: group.rateMultiplier,
      enableStickySession: group.enableStickySession,
      stickySessionExpirationHours: group.stickySessionExpirationHours,
      accounts: group.accounts.map(account => ({
        accountTokenId: account.accountTokenId,
        weight: account.weight,
        priority: account.priority
      }))
    };
  }

  private buildAccountDisplayMap(group: ProviderGroupOutputDto): Record<string, { name: string; label: string }> {
    return Object.fromEntries(
      group.accounts.map(account => [
        account.accountTokenId,
        {
          name: account.accountTokenName,
          label: getProviderAuthLabel(account.provider, account.authMethod)
        }
      ])
    );
  }

  onHide() {
    this.visible = false;
    this.visibleChange.emit(false);
    this.formModel.set(this.createEmptyGroup());
    this.accountDisplayMap.set({});
    this.isEditMode.set(false);
    this.selectedAccountsToAdd = [];
    this.availableAccounts.set([]);
  }

  loadAvailableAccounts() {
    this.accountService
      .getAccounts({
        offset: 0,
        limit: 1000,
        sorting: 'provider asc, authMethod asc, name asc'
      })
      .subscribe(result => {
        this.availableAccounts.set(result.items);
      });
  }

  isAccountAdded(accountId: string): boolean {
    return this.formModel().accounts.some(account => account.accountTokenId === accountId);
  }

  addAccount() {
    if (!this.selectedAccountsToAdd.length) return;

    const newAccounts = this.selectedAccountsToAdd
      .map(id => this.availableAccounts().find(account => account.id === id))
      .filter((account): account is AccountTokenOutputDto => !!account && !this.isAccountAdded(account.id))
      .map(
        account =>
          ({
            accountTokenId: account.id,
            weight: 1,
            priority: 0
          }) satisfies AddGroupAccountInputDto
      );

    if (newAccounts.length) {
      this.formModel.update(model => ({ ...model, accounts: [...model.accounts, ...newAccounts] }));
    }
    this.selectedAccountsToAdd = [];
  }

  getGroupSelectionState(group: GroupedAccountOption): GroupSelectionState {
    const ids = group.items.map(item => item.id);
    const selectedCount = ids.filter(id => this.selectedAccountsToAdd.includes(id)).length;

    if (!selectedCount) {
      return 'none';
    }

    return selectedCount === ids.length ? 'all' : 'partial';
  }

  toggleGroupSelection(group: GroupedAccountOption) {
    const groupIds = group.items.map(item => item.id);
    const selected = new Set(this.selectedAccountsToAdd);
    const isAllSelected = groupIds.every(id => selected.has(id));

    if (isAllSelected) {
      this.selectedAccountsToAdd = this.selectedAccountsToAdd.filter(id => !groupIds.includes(id));
      return;
    }

    groupIds.forEach(id => selected.add(id));
    this.selectedAccountsToAdd = Array.from(selected);
  }

  getGroupToggleLabel(group: GroupedAccountOption): string {
    const state = this.getGroupSelectionState(group);
    if (state === 'all') {
      return '取消全选';
    }

    if (state === 'partial') {
      return '补齐全选';
    }

    return '全选';
  }

  getAccountRouteLabelSummary(account: AccountTokenOutputDto): string {
    return this.getSupportedRouteLabels(account).join(' / ');
  }

  getGroupRouteLabelSummary(group: GroupedAccountOption): string {
    return group.routeLabels.join(' / ');
  }

  removeAccount(index: number) {
    const accounts = [...this.formModel().accounts];
    accounts.splice(index, 1);
    this.formModel.update(model => ({ ...model, accounts }));
  }

  getAccountName(id: string): string {
    const inAvailable = this.availableAccounts().find(account => account.id === id);
    if (inAvailable) return inAvailable.name;

    return this.accountDisplayMap()[id]?.name || id;
  }

  getAccountPlatformLabel(id: string): string {
    const account = this.availableAccounts().find(item => item.id === id);
    if (account) return getProviderAuthLabel(account.provider, account.authMethod);

    return this.accountDisplayMap()[id]?.label || '';
  }

  getSupportedRouteLabels(account: AccountTokenOutputDto): string[] {
    return Object.entries(ROUTE_PROFILE_SUPPORTED_COMBINATIONS)
      .filter(([, combinations]) => combinations.some(item => item.provider === account.provider && item.authMethod === account.authMethod))
      .map(([profile]) => ROUTE_PROFILE_LABELS[profile as RouteProfile]);
  }

  getAccountRouteSummary(id: string): string {
    const account = this.availableAccounts().find(item => item.id === id);
    if (!account) {
      return '';
    }

    return this.getSupportedRouteLabels(account).join(' / ');
  }

  isValid(): boolean {
    const model = this.formModel();

    if (!model.name) {
      return false;
    }

    if (model.name.length < 1 || model.name.length > 256) {
      return false;
    }

    if (model.description && model.description.length > 1000) {
      return false;
    }

    if (!model.rateMultiplier || model.rateMultiplier < 0.01 || model.rateMultiplier > 100) {
      return false;
    }

    if (model.enableStickySession) {
      if (!model.stickySessionExpirationHours || model.stickySessionExpirationHours < 1 || model.stickySessionExpirationHours > 8760) {
        return false;
      }
    }

    return true;
  }

  getStrategyDescription(strategy: GroupSchedulingStrategy): string {
    return SCHEDULING_STRATEGY_DESCRIPTIONS[strategy] || '';
  }

  save() {
    if (this.isValid()) {
      this.saved.emit({ ...this.formModel(), accounts: [...this.formModel().accounts] });
    }
  }
}
