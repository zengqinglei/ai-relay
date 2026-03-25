import { CommonModule } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs/operators';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';

import { ApiKeyOutputDto, SubscriptionMetricsOutputDto } from '../../models/subscription.dto';
import { SubscriptionMetricService } from '../../services/subscription-metric-service'; // Import
import { SubscriptionService } from '../../services/subscription-service';
import { SubscriptionEditDialogComponent } from './widgets/subscription-edit-dialog/subscription-edit-dialog';
import { SubscriptionMetricsCards } from './widgets/subscription-metrics-cards/subscription-metrics-cards';
import { SubscriptionTable } from './widgets/subscription-table/subscription-table';
import { LayoutService } from '../../../../layout/services/layout-service';

@Component({
  selector: 'app-subscriptions',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    SubscriptionTable,
    SubscriptionEditDialogComponent,
    SubscriptionMetricsCards,
    ConfirmDialogModule,
    SelectModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    ButtonModule
  ],
  providers: [ConfirmationService],
  templateUrl: './subscriptions.html'
})
export class SubscriptionsPage implements OnInit {
  private service = inject(SubscriptionService);
  private metricService = inject(SubscriptionMetricService); // Inject
  private destroyRef = inject(DestroyRef);
  private confirmationService = inject(ConfirmationService);
  private messageService = inject(MessageService);
  private layoutService = inject(LayoutService);

  subscriptions = signal<ApiKeyOutputDto[]>([]);
  totalRecords = signal(0);
  metrics = signal<SubscriptionMetricsOutputDto>({
    totalSubscriptions: 0,
    activeSubscriptions: 0,
    expiringSoon: 0,
    totalUsageToday: 0,
    usageGrowthRate: 0,
    topUsageKeys: []
  });
  loading = signal(false);

  // Dialogs
  editDialogVisible = signal(false);
  editDialogLoading = signal(false); // Loading for fetching subscription details
  editDialogSaving = signal(false); // Loading for saving operation
  selectedSubscription = signal<ApiKeyOutputDto | null>(null);

  // Filters
  searchQuery = signal('');
  selectedStatus = signal<'active' | 'inactive' | null>(null);

  // Pagination
  offset = signal(0);
  limit = signal(10);
  sorting = signal<string>('creationTime desc');

  statusOptions = [
    { label: '启用', value: 'active' },
    { label: '禁用', value: 'inactive' }
  ];

  private searchSubject = new Subject<string>();

  constructor() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.onFilter());
  }

  ngOnInit() {
    this.layoutService.title.set('订阅管理');
    // 只加载 metrics，列表由表格 lazy loading 触发
    this.metricService
      .getMetrics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(data => this.metrics.set(data));
  }

  loadData() {
    // 已废弃：改为只加载 metrics
    this.metricService
      .getMetrics()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(data => this.metrics.set(data));
  }

  reloadList() {
    this.loading.set(true);
    let isActive: boolean | undefined = undefined;
    if (this.selectedStatus() === 'active') isActive = true;
    if (this.selectedStatus() === 'inactive') isActive = false;

    this.service
      .getSubscriptions({
        keyword: this.searchQuery(),
        isActive: isActive,
        offset: this.offset(),
        limit: this.limit(),
        sorting: this.sorting()
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(data => {
        this.subscriptions.set(data.items);
        this.totalRecords.set(data.totalCount);
      });
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onStatusChange() {
    this.onFilter();
  }

  onFilter() {
    this.offset.set(0); // Reset to first page
    this.reloadList();
  }

  onPageChange(event: { offset: number; limit: number; sorting?: string }) {
    this.offset.set(event.offset);
    this.limit.set(event.limit);
    if (event.sorting) this.sorting.set(event.sorting);
    this.reloadList();
  }

  openAddDialog() {
    this.selectedSubscription.set(null);
    this.editDialogVisible.set(true);
  }

  openEditDialog(id: string) {
    this.selectedSubscription.set(null);
    this.editDialogVisible.set(true);
    this.editDialogLoading.set(true);

    this.service
      .getSubscription(id)
      .pipe(finalize(() => this.editDialogLoading.set(false)))
      .subscribe(sub => {
        this.selectedSubscription.set(sub);
      });
  }

  handleSave(dto: any) {
    this.editDialogSaving.set(true);

    if (this.selectedSubscription()) {
      // Update
      const { name: _name, ...rest } = dto;
      const updateDto = { ...rest, id: this.selectedSubscription()!.id };
      this.service
        .updateSubscription(updateDto.id, updateDto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '订阅更新成功' });
            this.editDialogVisible.set(false);
            this.reloadList();
          }
        });
    } else {
      // Create
      this.service
        .createSubscription(dto)
        .pipe(
          takeUntilDestroyed(this.destroyRef),
          finalize(() => this.editDialogSaving.set(false))
        )
        .subscribe({
          next: () => {
            this.messageService.add({ severity: 'success', summary: '成功', detail: '订阅创建成功' });
            this.editDialogVisible.set(false);
            this.reloadList();
            // Update metrics
            this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
          }
        });
    }
  }

  handleDelete(id: string) {
    this.confirmationService.confirm({
      message: '确定要删除此订阅吗？这将导致使用此 Key 的应用立即失效。',
      header: '确认删除',
      icon: 'pi pi-exclamation-triangle',
      accept: () => {
        this.service.deleteSubscription(id).subscribe(() => {
          this.messageService.add({ severity: 'success', summary: '成功', detail: '订阅已删除' });
          this.reloadList();
          this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
        });
      }
    });
  }

  handleStatusToggle(event: { id: string; isActive: boolean }) {
    this.service.toggleStatus(event.id, event.isActive).subscribe(() => {
      this.messageService.add({
        severity: 'success',
        summary: '成功',
        detail: `订阅已${event.isActive ? '启用' : '禁用'}`
      });
      this.reloadList();
      this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
    });
  }

  handleExpiryUpdate(event: { id: string; date: Date }) {
    // Direct atomic update: Enable with new expiry date
    this.service.toggleStatus(event.id, true, event.date).subscribe(() => {
      this.messageService.add({
        severity: 'success',
        summary: '成功',
        detail: '已更新过期时间并启用订阅'
      });
      this.reloadList();
      this.metricService.getMetrics().subscribe(data => this.metrics.set(data));
    });
  }
}
