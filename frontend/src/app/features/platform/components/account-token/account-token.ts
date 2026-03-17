import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { finalize } from 'rxjs/operators';

import {
  CreateAccountTokenInputDto,
  GetAccountTokenPagedInputDto,
  AccountTokenMetricsOutputDto,
  AccountTokenOutputDto,
  UpdateAccountTokenInputDto
} from '../../models/account-token.dto';
import { AccountTokenMetricService } from '../../services/account-token-metric-service';
import { AccountTokenService } from '../../services/account-token-service';
import { AccountDetailDialogComponent } from './widgets/account-detail-dialog/account-detail-dialog';
import { AccountEditDialogComponent } from './widgets/account-edit-dialog/account-edit-dialog';
import { AccountMetricsCards } from './widgets/account-metrics-cards/account-metrics-cards';
import { AccountTable } from './widgets/account-table/account-table';
import { LayoutService } from '../../../../layout/services/layout-service';

@Component({
  selector: 'app-account-token',
  standalone: true,
  imports: [CommonModule, AccountMetricsCards, AccountTable, AccountEditDialogComponent, AccountDetailDialogComponent, ConfirmDialogModule],
  providers: [ConfirmationService],
  templateUrl: './account-token.html'
})
export class AccountTokenPage implements OnInit {
  private service = inject(AccountTokenService);
  private metricService = inject(AccountTokenMetricService);
  private destroyRef = inject(DestroyRef);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private layoutService = inject(LayoutService);

  // State
  accounts = signal<AccountTokenOutputDto[]>([]);
  totalCount = signal(0);
  metrics = signal<AccountTokenMetricsOutputDto>({
    totalAccounts: 0,
    activeAccounts: 0,
    disabledAccounts: 0,
    expiringAccounts: 0,
    totalUsageToday: 0,
    usageGrowthRate: 0,
    averageSuccessRate: 0,
    abnormalRequests24h: 0,
    rotationWarnings: 0
  });

  loading = signal(false);

  // Dialog State
  editDialogVisible = signal(false);
  detailDialogVisible = signal(false);
  selectedAccount = signal<AccountTokenOutputDto | null>(null);
  editDialogSaving = signal(false);

  // Filter State
  currentFilter = signal<GetAccountTokenPagedInputDto>({
    offset: 0,
    limit: 10
  });

  ngOnInit() {
    this.layoutService.title.set('渠道账号');
    // 移除 loadData()，让表格的 lazy loading 触发首次加载
    // 但仍需加载 metrics
    this.metricService
      .getMetrics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(data => this.metrics.set(data));
  }

  loadData() {
    this.loading.set(true);

    // Load accounts
    this.service
      .getAccounts(this.currentFilter())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(data => {
        this.accounts.set(data.items);
        this.totalCount.set(data.totalCount);
      });

    // Load metrics (Parallel load)
    this.metricService
      .getMetrics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(data => this.metrics.set(data));
  }

  onFilterChange(filter: GetAccountTokenPagedInputDto) {
    this.currentFilter.set(filter);
    // Reload only list when filter changes, usually metrics don't change with list filter unless metrics are also filtered
    // Current design: metrics are global.
    this.reloadList();
  }

  reloadList() {
    this.loading.set(true);
    this.service
      .getAccounts(this.currentFilter())
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(data => {
        this.accounts.set(data.items);
        this.totalCount.set(data.totalCount);
      });
  }

  // --- Actions ---

  openAddDialog() {
    this.selectedAccount.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    const acc = this.accounts().find(a => a.id === id);
    if (acc) {
      this.selectedAccount.set(acc);
      this.editDialogVisible.set(true);
    }
  }

  openDetailDialog(id: string) {
    const acc = this.accounts().find(a => a.id === id);
    if (acc) {
      this.selectedAccount.set(acc);
      this.detailDialogVisible.set(true);
    }
  }

  handleSave(data: { id?: string; dto: CreateAccountTokenInputDto | UpdateAccountTokenInputDto }) {
    this.editDialogSaving.set(true);

    if (data.id) {
      // Update
      this.service
        .updateAccount(data.id, data.dto as UpdateAccountTokenInputDto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '账户更新成功' });
            this.editDialogVisible.set(false);
            this.reloadList();
          }
        });
    } else {
      // Create
      this.service
        .createAccount(data.dto as CreateAccountTokenInputDto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '账户创建成功' });
            this.editDialogVisible.set(false);
            this.reloadList();
            // Update metrics as well since count changed
            this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
          }
        });
    }
  }

  handleDelete(id: string) {
    this.confirmationService.confirm({
      message: '确定要删除该账户吗？删除后不可恢复。',
      header: '确认删除',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.deleteAccount(id).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '账户已删除' });
          this.reloadList();
          this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
        });
      }
    });
  }

  handleStatusToggle(event: { accountId: string; isActive: boolean }) {
    const serviceCall = event.isActive ? this.service.enableAccount(event.accountId) : this.service.disableAccount(event.accountId);

    serviceCall.subscribe(() => {
      this.messageService.add({
        severity: 'success',

        summary: '成功',

        detail: `账户已${event.isActive ? '启用' : '禁用'}`
      });

      this.reloadList();

      this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
    });
  }

  handleResetStatus(id: string) {
    this.service.resetStatus(id).subscribe(() => {
      this.messageService.add({ severity: 'success', summary: '成功', detail: '已重置账户状态' });

      this.reloadList();
    });
  }
}
