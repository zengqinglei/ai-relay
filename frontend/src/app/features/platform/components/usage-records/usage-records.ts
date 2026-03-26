import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, finalize } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { UsageRecordDetailDialog } from './usage-record-detail-dialog';
import { LayoutService } from '../../../../layout/services/layout-service';
import { PROVIDER_PLATFORM_LABELS } from '../../../../shared/constants/provider-platform.constants';
import { PagedResultDto } from '../../../../shared/models/paged-result.dto';
import { ProviderPlatform } from '../../../../shared/models/provider-platform.enum';
import { UsageStatus } from '../../../../shared/models/usage-status.enum';
import { HttpStatusSeverityPipe } from '../../../../shared/pipes/http-status-severity.pipe';
import { formatTokenCount } from '../../../../shared/utils/format.utils';
import { ProviderGroupOutputDto } from '../../models/provider-group.dto';
import { UsageRecordOutputDto, UsageRecordPagedInputDto } from '../../models/usage.dto';
import { ProviderGroupService } from '../../services/provider-group-service';
import { UsageRecordService } from '../../services/usage-record-service';
import { FilterStateService } from '../../../../shared/services/filter-state.service';

@Component({
  selector: 'app-usage-records',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    TableModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    DatePickerModule,
    TagModule,
    TooltipModule,
    IconFieldModule,
    InputIconModule,
    PaginatorModule,
    UsageRecordDetailDialog,
    HttpStatusSeverityPipe
  ],
  templateUrl: './usage-records.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsageRecords implements OnInit {
  private readonly usageRecordService = inject(UsageRecordService);
  private readonly providerGroupService = inject(ProviderGroupService);
  private readonly layoutService = inject(LayoutService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly filterStateService = inject(FilterStateService);
  private readonly textFilterSubject = new Subject<void>();

  private readonly FILTER_KEY = 'usage-records';

  // Data Signals
  records = signal<UsageRecordOutputDto[]>([]);
  totalRecords = signal<number>(0);
  loading = signal<boolean>(false);

  // Filter Options
  groups = signal<ProviderGroupOutputDto[]>([]);

  platforms = Object.values(ProviderPlatform).map(value => ({
    label: PROVIDER_PLATFORM_LABELS[value],
    value: value
  }));

  // Filters
  filterApiKeyName = signal<string>('');
  filterModel = signal<string>('');
  filterAccountTokenName = signal<string>('');
  filterProviderGroupId = signal<string | null>(null);
  filterPlatform = signal<ProviderPlatform | null>(null);
  filterStartTime = signal<Date | null>(null);
  filterEndTime = signal<Date | null>(null);

  // Pagination & Sorting
  first = signal<number>(0);
  rows = signal<number>(10);
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1); // -1 for desc, 1 for asc

  // Detail Dialog
  detailDialogVisible = signal<boolean>(false);
  selectedRecord = signal<UsageRecordOutputDto | null>(null);

  ngOnInit() {
    this.layoutService.title.set('使用记录');
    this.loadFilterOptions();

    const saved = this.filterStateService.load<{ platform: ProviderPlatform | null; providerGroupId: string | null }>(this.FILTER_KEY);
    if (saved.platform !== undefined) this.filterPlatform.set(saved.platform ?? null);
    if (saved.providerGroupId !== undefined) this.filterProviderGroupId.set(saved.providerGroupId ?? null);

    this.textFilterSubject.pipe(
      debounceTime(300),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.first.set(0);
      this.loadData();
    });
  }

  onTextFilterChange() {
    this.textFilterSubject.next();
  }

  onSelectFilterChange() {
    this.filterStateService.save(this.FILTER_KEY, {
      platform: this.filterPlatform(),
      providerGroupId: this.filterProviderGroupId()
    });
    this.first.set(0);
    this.loadData();
  }

  loadFilterOptions() {
    this.providerGroupService.getAll().subscribe(res => {
      this.groups.set(res);
    });
  }

  loadData() {
    this.loading.set(true);

    const input: UsageRecordPagedInputDto = {
      offset: this.first(),
      limit: this.rows(),
      sorting: this.getSorting(),
      apiKeyName: this.filterApiKeyName() || undefined,
      model: this.filterModel() || undefined,
      accountTokenName: this.filterAccountTokenName() || undefined,
      providerGroupId: this.filterProviderGroupId() || undefined,
      platform: this.filterPlatform() || undefined,
      startTime: this.filterStartTime() ? this.filterStartTime()!.toISOString() : undefined,
      endTime: this.filterEndTime() ? this.filterEndTime()!.toISOString() : undefined
    };

    this.usageRecordService
      .getUsageRecords(input)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe((res: PagedResultDto<UsageRecordOutputDto>) => {
        this.records.set(res.items);
        this.totalRecords.set(res.totalCount);
      });
  }

  onLazyLoad(event: TableLazyLoadEvent) {
    this.first.set(event.first ?? 0);
    this.rows.set(event.rows ?? 10);

    if (event.sortField) {
      this.sortField.set(Array.isArray(event.sortField) ? event.sortField[0] : event.sortField);
      this.sortOrder.set(event.sortOrder ?? -1);
    }

    this.loadData();
  }

  resetFilters() {
    this.filterApiKeyName.set('');
    this.filterModel.set('');
    this.filterAccountTokenName.set('');
    this.filterProviderGroupId.set(null);
    this.filterPlatform.set(null);
    this.filterStartTime.set(null);
    this.filterEndTime.set(null);

    // Reset pagination
    this.first.set(0);

    this.loadData();
  }

  setDateRange(range: 'today' | 'week' | 'month') {
    const end = new Date();
    const start = new Date();

    // Reset hours to start of day for start date, end of day for end date?
    // Usually quick select sets full days.
    end.setHours(23, 59, 59, 999);
    start.setHours(0, 0, 0, 0);

    if (range === 'today') {
      // Start is already today 00:00
    } else if (range === 'week') {
      start.setDate(start.getDate() - 6); // Last 7 days including today
    } else if (range === 'month') {
      start.setMonth(start.getMonth() - 1);
    }

    this.filterStartTime.set(start);
    this.filterEndTime.set(end);
    this.loadData();
  }

  openDetail(record: UsageRecordOutputDto) {
    this.selectedRecord.set(record);
    this.detailDialogVisible.set(true);
  }

  private getSorting(): string {
    let field = this.sortField();
    const order = this.sortOrder() === 1 ? 'asc' : 'desc';

    // Handle special sorting fields mapping
    if (field === 'token') {
      field = 'InputTokens + OutputTokens';
    }

    return `${field} ${order}`;
  }

  getStatusSeverity(status: string): 'success' | 'danger' | 'info' | 'warn' | undefined {
    switch (status) {
      case UsageStatus.Success:
        return 'success';
      case UsageStatus.Failed:
        return 'danger';
      case UsageStatus.InProgress:
        return 'info';
      default:
        return 'info';
    }
  }

  getStatusLabel(status: string): string {
    switch (status) {
      case UsageStatus.InProgress:
        return '进行中';
      case UsageStatus.Success:
        return '成功';
      case UsageStatus.Failed:
        return '失败';
      default:
        return '未知';
    }
  }

  getPlatformLabel(platform: ProviderPlatform): string {
    return PROVIDER_PLATFORM_LABELS[platform] || `Unknown (${platform})`;
  }

  getHttpStatusCode(record: UsageRecordOutputDto): string {
    return record.upStatusCode?.toString() || '200';
  }

  /**
   * 判断是否显示失败详情图标
   */
  shouldShowFailureDetail(record: UsageRecordOutputDto): boolean {
    return record.status === UsageStatus.Failed && !!record.statusDescription;
  }

  /**
   * 格式化 Token 数量（K, M, B）
   */
  formatTokenCount(num: number | null | undefined): string {
    return formatTokenCount(num || 0);
  }

  /**
   * 获取模型显示文本
   */
  getModelDisplay(record: UsageRecordOutputDto): string {
    const down = record.downModelId || 'N/A';
    const up = record.upModelId || 'N/A';
    return down === up ? down : `down:${down} up:${up}`;
  }
}
