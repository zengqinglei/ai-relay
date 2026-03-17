import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, EventEmitter, input, Output, signal, inject, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ConfirmationService, MenuItem } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { MenuModule } from 'primeng/menu';
import { SelectModule } from 'primeng/select';
import { SkeletonModule } from 'primeng/skeleton';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { PROVIDER_PLATFORM_OPTIONS } from '../../../../../../shared/constants/provider-platform.constants';
import { ProviderPlatform } from '../../../../../../shared/models/provider-platform.enum';
import { PlatformLabelPipe } from '../../../../../../shared/pipes/platform-label-pipe';
import { GetAccountTokenPagedInputDto, AccountTokenOutputDto, AccountStatus } from '../../../../models/account-token.dto';
import { ModelTestDialog } from '../model-test-dialog/model-test-dialog';

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
    PlatformLabelPipe,
    ModelTestDialog,
    PlatformIcon
  ],
  templateUrl: './account-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class AccountTable {
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

  // Filter states
  searchQuery = signal('');
  selectedPlatform = signal<ProviderPlatform | null>(null);
  selectedStatus = signal<'active' | 'inactive' | null>(null);

  // Menu
  menuItems = signal<MenuItem[]>([]);

  // Dropdown options
  platformOptions = PROVIDER_PLATFORM_OPTIONS;

  statusOptions = [
    { label: '已启用', value: 'active' },
    { label: '已禁用', value: 'inactive' }
  ];

  ProviderPlatform = ProviderPlatform;
  AccountStatus = AccountStatus;

  // Pagination state
  first = 0;
  rows = 10;

  onFilter() {
    this.first = 0;
    this.emitFilterChange();
  }

  onPage(event: TableLazyLoadEvent) {
    this.first = event.first ?? 0;
    this.rows = event.rows ?? 10;
    this.emitFilterChange();
  }

  openTestDialog(account: AccountTokenOutputDto) {
    this.modelTestDialog.open(account);
  }

  showActionMenu(event: Event, menu: any, account: AccountTokenOutputDto) {
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

  getMaskedToken(token: string | undefined): string {
    if (!token || token.length < 12) return '***';
    return `${token.substring(0, 7)}...${token.substring(token.length - 4)}`;
  }

  private emitFilterChange() {
    let isActive: boolean | undefined = undefined;
    if (this.selectedStatus() === 'active') isActive = true;
    if (this.selectedStatus() === 'inactive') isActive = false;

    const filter: GetAccountTokenPagedInputDto = {
      offset: this.first,
      limit: this.rows,
      keyword: this.searchQuery() || undefined,
      platform: this.selectedPlatform() || undefined,
      isActive: isActive
    };
    this.filterChange.emit(filter);
  }

  // 简单判断是否过期或接近过期
  getExpiryState(account: AccountTokenOutputDto): 'expired' | 'warning' | 'ok' | 'forever' {
    if (!account.isActive) return 'forever'; // 禁用状态不展示过期警告
    if (account.expiresIn === null || account.expiresIn === undefined) return 'forever';

    // 注意：后端返回的 expiresIn 是剩余秒数 (Mock中模拟的是固定值，实际应结合 tokenObtainedTime 计算)
    // 这里假设 expiresIn 是剩余秒数。如果 mock 只是静态数据，我们需要重新计算

    const secondsLeft = account.expiresIn;

    // 如果 mock 数据中 tokenObtainedTime 是过去的，expiresIn 应该是以此为基准的有效期时长
    // 但 DTO 定义 expiresIn 为 "秒"，通常指"剩余有效期"或"有效期时长"。
    // 这里为了简单，假设 DTO 中的 expiresIn 是"剩余秒数" (snapshot) 或者我们根据 obtainedTime + duration 算。
    // 为适配 Mock 数据的简单性 (expiresIn = 3500 固定值), 我们直接用它判断

    if (secondsLeft <= 0) return 'expired';
    if (secondsLeft < 1800) return 'warning'; // 30min
    return 'ok';
  }

  formatExpiry(seconds: number): string {
    const h = (seconds / 3600).toFixed(1);
    return `${h}h`;
  }

  formatMinutes(seconds: number): string {
    return Math.floor(seconds / 60).toString();
  }

  formatDuration(seconds?: number): string {
    if (!seconds) return '-';
    if (seconds < 60) return `${seconds}秒`;
    if (seconds < 3600) return `${Math.floor(seconds / 60)}分${seconds % 60 > 0 ? `${seconds % 60}秒` : ''}`;
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    return `${h}小时${m > 0 ? `${m}分` : ''}`;
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
    if (seconds < 60) return `${seconds}秒后解除`;
    if (seconds < 3600) {
      const minutes = Math.ceil(seconds / 60);
      return `${minutes}分钟后解除`;
    }
    const hours = Math.floor(seconds / 3600);
    const minutes = Math.ceil((seconds % 3600) / 60);
    if (minutes === 0) {
      return `${hours}小时后解除`;
    }
    return `${hours}小时${minutes}分钟后解除`;
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
    const statusText = account.status === AccountStatus.RateLimited ? '限流' : '异常';
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
      case AccountStatus.Error:
        return '异常';
      default:
        return '未知';
    }
  }

  shouldShowStatusDetail(account: AccountTokenOutputDto): boolean {
    return (account.status === AccountStatus.Error || account.status === AccountStatus.RateLimited) && !!account.statusDescription;
  }

  getStatusIconColor(status: AccountStatus): string {
    switch (status) {
      case AccountStatus.RateLimited:
        return 'text-orange-500';
      case AccountStatus.Error:
        return 'text-red-500';
      default:
        return 'text-muted-color';
    }
  }
}
