import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, inject, input, OnInit, Output, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { MultiSelectModule } from 'primeng/multiselect';
import { Popover, PopoverModule } from 'primeng/popover';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { AUTH_METHOD_OPTIONS, PROVIDER_OPTIONS } from '../../../../../../shared/constants/provider.constants';
import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { AuthMethod } from '../../../../../../shared/models/auth-method.enum';
import { Provider } from '../../../../../../shared/models/provider.enum';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { AuthMethodLabelPipe } from '../../../../../../shared/pipes/auth-method-label.pipe';
import { ProviderLabelPipe } from '../../../../../../shared/pipes/platform-label-pipe';
import { FilterStateService } from '../../../../../../shared/services/filter-state.service';
import { formatDurationVerbose, formatTokenCount } from '../../../../../../shared/utils/format.utils';
import {
  AccountStatus,
  AccountTokenOutputDto,
  GetAccountTokenPagedInputDto,
  LimitedModelStateDto
} from '../../../../models/account-token.dto';
import { ProviderGroupOutputDto } from '../../../../models/provider-group.dto';
import { ProviderGroupService } from '../../../../services/provider-group-service';
import {
  RelationPopoverContentComponent,
  RelationPopoverItem
} from '../../../shared/widgets/relation-popover-content/relation-popover-content';
import { ModelTestDialog } from '../model-test-dialog/model-test-dialog';

type AccountPopoverMode = 'details' | 'summary' | 'limited-models';

@Component({
  selector: 'app-account-table',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    TagModule,
    TooltipModule,
    SkeletonModule,
    ToggleSwitchModule,
    IconFieldModule,
    InputIconModule,
    ConfirmPopupModule,
    MenuModule,
    MultiSelectModule,
    PopoverModule,
    ProviderLabelPipe,
    AuthMethodLabelPipe,
    ModelTestDialog,
    PlatformIcon,
    RelationPopoverContentComponent
  ],
  templateUrl: './account-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class AccountTable implements OnInit {
  accounts = input.required<AccountTokenOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly filterChange = new EventEmitter<GetAccountTokenPagedInputDto>();
  @Output() readonly add = new EventEmitter<void>();
  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();
  @Output() readonly viewDetail = new EventEmitter<string>();
  @Output() readonly statusToggle = new EventEmitter<{ accountId: string; isActive: boolean }>();
  @Output() readonly resetStatus = new EventEmitter<string>();

  @ViewChild(ModelTestDialog) modelTestDialog!: ModelTestDialog;

  private confirmationService = inject(ConfirmationService);
  private filterStateService = inject(FilterStateService);
  private providerGroupService = inject(ProviderGroupService);
  private searchSubject = new Subject<string>();

  private readonly FILTER_KEY = 'account-token';

  // Filter states
  searchQuery = signal('');
  selectedProvider = signal<Provider | null>(null);
  selectedAuthMethod = signal<AuthMethod | null>(null);
  selectedStatus = signal<'active' | 'inactive' | null>(null);
  selectedProviderGroupIds = signal<string[]>([]);

  // Menu
  menuItems = signal<MenuItem[]>([]);

  // Dropdown options
  providerOptions = PROVIDER_OPTIONS;
  authMethodOptions = AUTH_METHOD_OPTIONS;
  providerGroupOptions = signal<ProviderGroupOutputDto[]>([]);

  statusOptions = [
    { label: '已启用', value: 'active' },
    { label: '已禁用', value: 'inactive' }
  ];

  AccountStatus = AccountStatus;

  // Pagination state
  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);
  activeProviderGroups = signal<ProviderGroupOutputDto[]>([]);
  activeLimitedModels = signal<LimitedModelStateDto[]>([]);
  groupPopoverMode = signal<AccountPopoverMode>('summary');
  visibleGroupCount = signal(1);

  constructor() {
    this.searchSubject.pipe(debounceTime(300), distinctUntilChanged()).subscribe(() => this.onFilter());
  }

  ngOnInit() {
    const saved = this.filterStateService.load<{
      keyword: string;
      provider: Provider | null;
      authMethod: AuthMethod | null;
      isActive: boolean | null;
      providerGroupIds: string[];
    }>(this.FILTER_KEY);
    if (saved.keyword) this.searchQuery.set(saved.keyword);
    if (saved.provider) this.selectedProvider.set(saved.provider);
    if (saved.authMethod) this.selectedAuthMethod.set(saved.authMethod);
    if (saved.providerGroupIds?.length) this.selectedProviderGroupIds.set(saved.providerGroupIds);

    if (saved.isActive === true) this.selectedStatus.set('active');
    else if (saved.isActive === false) this.selectedStatus.set('inactive');

    this.providerGroupService.getAll().subscribe(groups => this.providerGroupOptions.set(groups));
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onSelectChange() {
    this.onFilter();
  }

  onFilter() {
    this.first = 0;
    this.emitFilterChange();
  }

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    if (event.sortField) {
      this.sortField.set(Array.isArray(event.sortField) ? event.sortField[0] : event.sortField);
      this.sortOrder.set(event.sortOrder ?? -1);
    }
    this.emitFilterChange();
  }

  openTestDialog(account: AccountTokenOutputDto) {
    this.modelTestDialog.open(account);
  }

  showActionMenu(event: Event, menu: { toggle: (event: Event) => void }, account: AccountTokenOutputDto) {
    const isRateLimited = account.status === AccountStatus.RateLimited;

    // Base items (Detail, Delete) always in menu
    const items: MenuItem[] = [];

    // If RateLimited, "Edit" is pushed to menu because "Clear" takes the slot
    if (isRateLimited) {
      items.push({
        label: '编辑',
        icon: 'pi pi-pencil',
        command: () => this.edit.emit(account.id)
      });
    }

    items.push({
      label: '查看详情',
      icon: 'pi pi-info-circle',
      command: () => this.viewDetail.emit(account.id)
    });

    items.push({
      separator: true
    });

    items.push({
      label: '删除',
      icon: 'pi pi-trash',
      styleClass: 'text-red-500',
      command: () => this.delete.emit(account.id)
    });

    this.menuItems.set(items);
    menu.toggle(event);
  }

  private emitFilterChange() {
    let isActive: boolean | undefined = undefined;
    if (this.selectedStatus() === 'active') isActive = true;
    if (this.selectedStatus() === 'inactive') isActive = false;

    this.filterStateService.save(this.FILTER_KEY, {
      keyword: this.searchQuery(),
      provider: this.selectedProvider(),
      authMethod: this.selectedAuthMethod(),
      isActive,
      providerGroupIds: this.selectedProviderGroupIds()
    });

    const filter: GetAccountTokenPagedInputDto = {
      offset: this.first,
      limit: this.rows,
      keyword: this.searchQuery() || undefined,
      provider: this.selectedProvider() || undefined,
      authMethod: this.selectedAuthMethod() || undefined,
      isActive,
      providerGroupIds: this.selectedProviderGroupIds().length ? this.selectedProviderGroupIds() : undefined,
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    };
    this.filterChange.emit(filter);
  }

  getAccountGroups(account: AccountTokenOutputDto): ProviderGroupOutputDto[] {
    const groupMap = new Map(this.providerGroupOptions().map(group => [group.id, group]));
    return account.providerGroupIds.map(id => groupMap.get(id)).filter((group): group is ProviderGroupOutputDto => !!group);
  }

  getVisibleGroups(account: AccountTokenOutputDto): ProviderGroupOutputDto[] {
    return this.getAccountGroups(account).slice(0, this.visibleGroupCount());
  }

  getHiddenGroups(account: AccountTokenOutputDto): ProviderGroupOutputDto[] {
    return this.getAccountGroups(account).slice(this.visibleGroupCount());
  }

  openGroupDetailsPopover(event: Event, popover: Popover, group: ProviderGroupOutputDto) {
    this.groupPopoverMode.set('details');
    this.activeProviderGroups.set([group]);
    popover.toggle(event);
  }

  openGroupSummaryPopover(event: Event, popover: Popover, groups: ProviderGroupOutputDto[]) {
    this.groupPopoverMode.set('summary');
    this.activeProviderGroups.set(groups);
    popover.toggle(event);
  }

  openLimitedModelsPopover(event: Event, popover: Popover, account: AccountTokenOutputDto) {
    this.groupPopoverMode.set('limited-models');
    this.activeLimitedModels.set(account.limitedModels ?? []);
    popover.toggle(event);
  }

  getGroupPopoverItems(): RelationPopoverItem[] {
    if (this.groupPopoverMode() === 'limited-models') {
      return this.activeLimitedModels().map(model => ({
        id: model.modelKey,
        leftText: model.displayName || model.modelKey,
        rightText: model.lockedUntil
          ? this.formatRemainingTime(this.getRemainingSeconds(model.lockedUntil))
          : model.statusDescription || '限流中',
        isWarning: true
      }));
    }

    if (this.groupPopoverMode() === 'details') {
      return this.activeProviderGroups().flatMap(group => {
        if (!group.supportedRouteProfiles?.length) {
          return [
            {
              id: `${group.id}-empty`,
              leftText: group.name,
              rightText: '空分组',
              isWarning: true
            }
          ];
        }

        return group.supportedRouteProfiles.map(profile => ({
          id: `${group.id}-${profile}`,
          leftText: this.getRouteProfileLabel(profile),
          rightText: this.getRouteProfilePath(profile)
        }));
      });
    }

    return this.activeProviderGroups().map(group => ({
      id: group.id,
      leftText: group.name,
      rightText: this.getGroupRouteBadgeSummary(group),
      isWarning: !group.supportedRouteProfiles?.length
    }));
  }

  getGroupRouteBadgeSummary(group: ProviderGroupOutputDto): string {
    if (!group.supportedRouteProfiles?.length) {
      return '空分组';
    }

    return group.supportedRouteProfiles.map(profile => this.getRouteProfileLabel(profile)).join(' | ');
  }

  getRouteProfilePath(profile: RouteProfile): string {
    const fullLabel = this.getRouteProfileFullLabel(profile);
    const match = /\(([^)]+)\)$/.exec(fullLabel);
    return match?.[1] ?? fullLabel;
  }

  getRouteProfileLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_LABELS[profile] || profile;
  }

  getRouteProfileFullLabel(profile: RouteProfile): string {
    return ROUTE_PROFILE_FULL_LABELS[profile] || profile;
  }

  // 简单判断是否过期或接近过期
  getExpiryState(account: AccountTokenOutputDto): 'expired' | 'warning' | 'ok' | 'forever' {
    if (!account.isActive) return 'forever'; // 禁用状态不展示过期警告
    if (account.expiresIn === null || account.expiresIn === undefined) return 'forever';

    const secondsLeft = account.expiresIn;

    if (secondsLeft <= 0) return 'expired';
    if (secondsLeft < 1800) return 'warning'; // 30min
    return 'ok';
  }

  formatMinutes(seconds: number): string {
    return Math.floor(seconds / 60).toString();
  }

  getLimitedModelCount(account: AccountTokenOutputDto): number {
    return account.limitedModelCount ?? account.limitedModels?.length ?? 0;
  }

  private getRemainingSeconds(lockedUntil: string): number {
    const unlockTime = new Date(lockedUntil).getTime();
    const now = Date.now();
    return Math.max(0, Math.floor((unlockTime - now) / 1000));
  }

  /**
   * 计算距离解封的剩余时间（秒）
   */
  getRateLimitRemainingSeconds(account: AccountTokenOutputDto): number {
    if (!account.lockedUntil) return 0;
    const unlockTime = new Date(account.lockedUntil).getTime();
    const now = Date.now();
    const remainingMs = unlockTime - now;
    return Math.max(0, Math.floor(remainingMs / 1000));
  }

  /**
   * 格式化剩余时间："XX分钟后解除"
   */
  formatRemainingTime(seconds: number): string {
    if (seconds <= 0) return '即将解除';
    return `${formatDurationVerbose(seconds * 1000)}后解除`;
  }

  /**
   * 格式化解封时间用于 tooltip
   */
  formatUnlockTime(lockedUntil: string): string {
    const date = new Date(lockedUntil);
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    const hours = String(date.getHours()).padStart(2, '0');
    const minutes = String(date.getMinutes()).padStart(2, '0');
    const seconds = String(date.getSeconds()).padStart(2, '0');
    return `将于 ${year}-${month}-${day} ${hours}:${minutes}:${seconds} 解除`;
  }

  confirmStatusToggle(event: Event, account: AccountTokenOutputDto) {
    this.confirmationService.confirm({
      target: event.target as EventTarget,
      message: `确定要${account.isActive ? '禁用' : '启用'}该账户吗？`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: '确定',
      rejectLabel: '取消',
      accept: () => {
        this.statusToggle.emit({ accountId: account.id, isActive: !account.isActive });
      }
    });
  }

  confirmResetStatus(event: Event, account: AccountTokenOutputDto) {
    const statusText =
      account.status === AccountStatus.RateLimited ? '限流' : account.status === AccountStatus.PartiallyRateLimited ? '部分限流' : '异常';
    this.confirmationService.confirm({
      target: event.target as EventTarget,
      message: `确定要重置该账户的${statusText}状态吗？`,
      icon: 'pi pi-refresh',
      acceptLabel: '确定',
      rejectLabel: '取消',
      accept: () => {
        this.resetStatus.emit(account.id);
      }
    });
  }

  getStatusSeverity(status: AccountStatus): 'success' | 'warn' | 'danger' | undefined {
    switch (status) {
      case AccountStatus.Normal:
        return 'success';
      case AccountStatus.RateLimited:
      case AccountStatus.PartiallyRateLimited:
        return 'warn';
      case AccountStatus.Error:
        return 'danger';
      default:
        return undefined;
    }
  }

  getStatusLabel(status: AccountStatus): string {
    switch (status) {
      case AccountStatus.Normal:
        return '正常';
      case AccountStatus.RateLimited:
        return '限流';
      case AccountStatus.PartiallyRateLimited:
        return '部分限流';
      case AccountStatus.Error:
        return '异常';
      default:
        return '未知';
    }
  }

  shouldShowStatusDetail(account: AccountTokenOutputDto): boolean {
    return (
      (account.status === AccountStatus.Error ||
        account.status === AccountStatus.RateLimited ||
        account.status === AccountStatus.PartiallyRateLimited) &&
      !!account.statusDescription
    );
  }

  getStatusIconColor(status: AccountStatus): string {
    switch (status) {
      case AccountStatus.RateLimited:
      case AccountStatus.PartiallyRateLimited:
        return 'text-orange-500';
      case AccountStatus.Error:
        return 'text-red-500';
      default:
        return 'text-muted-color';
    }
  }

  formatTokenCount = formatTokenCount;
}

