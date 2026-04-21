import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { LayoutService } from '../../../../layout/services/layout-service';
import { formatDuration, formatTokenCount } from '../../../../shared/utils/format.utils';
import { UsageStatus } from '../../../../shared/models/usage-status.enum';
import { UsageRecordOutputDto } from '../../../platform/models/usage.dto';
import { UsageRecordService } from '../../../platform/services/usage-record-service';
import { finalize } from 'rxjs/operators';
import { PagedResultDto } from '../../../../shared/models/paged-result.dto';

@Component({
  selector: 'app-workspace-usage-logs',
  standalone: true,
  imports: [ButtonModule, CommonModule, FormsModule, DatePickerModule, InputTextModule, SelectModule, TableModule, TagModule, TooltipModule],
  templateUrl: './workspace-usage-logs.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkspaceUsageLogsPage {
  private readonly layoutService = inject(LayoutService);
  private readonly usageRecordService = inject(UsageRecordService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly filterKeyword = signal('');
  readonly filterStatus = signal<string | null>(null);
  readonly filterStartTime = signal<Date | null>(null);
  readonly filterEndTime = signal<Date | null>(null);
  
  readonly logs = signal<UsageRecordOutputDto[]>([]);
  readonly totalRecords = signal<number>(0);
  
  readonly first = signal<number>(0);
  readonly rows = signal<number>(10);

  readonly statusOptions = [
    { label: '成功', value: UsageStatus.Success },
    { label: '失败', value: UsageStatus.Failed },
    { label: '处理中', value: UsageStatus.InProgress }
  ];

  constructor() {
    this.layoutService.title.set('使用日志');
    this.loadData();
  }

  reload() {
    this.first.set(0);
    this.loadData();
  }

  loadData() {
    this.loading.set(true);
    this.usageRecordService
      .getUsageRecords({
        offset: this.first(),
        limit: this.rows(),
        sorting: 'creationTime desc',
        keyword: this.filterKeyword().trim() || undefined,
        status: this.filterStatus() || undefined,
        startTime: this.filterStartTime() ? this.filterStartTime()!.toISOString() : undefined,
        endTime: this.filterEndTime() ? this.filterEndTime()!.toISOString() : undefined
      })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe((data: PagedResultDto<UsageRecordOutputDto>) => {
        this.logs.set(data.items);
        this.totalRecords.set(data.totalCount);
      });
  }

  onLazyLoad(event: TableLazyLoadEvent) {
    this.first.set(event.first ?? 0);
    this.rows.set(event.rows ?? 10);
    this.loadData();
  }

  setDateRange(range: 'today' | 'week' | 'month') {
    const end = new Date();
    const start = new Date();

    end.setHours(23, 59, 59, 999);
    start.setHours(0, 0, 0, 0);

    if (range === 'today') {
      // Start is already today 00:00
    } else if (range === 'week') {
      start.setDate(start.getDate() - 6);
    } else if (range === 'month') {
      start.setMonth(start.getMonth() - 1);
    }

    this.filterStartTime.set(start);
    this.filterEndTime.set(end);
    this.reload();
  }

  getStatusSeverity(status: string) {
    switch (status as UsageStatus) {
      case UsageStatus.Success:
        return 'success';
      case UsageStatus.Failed:
        return 'danger';
      case UsageStatus.InProgress:
        return 'warn';
      default:
        return 'secondary';
    }
  }

  getStatusLabel(status: string) {
    switch (status as UsageStatus) {
      case UsageStatus.Success:
        return '成功';
      case UsageStatus.Failed:
        return '失败';
      case UsageStatus.InProgress:
        return '处理中';
      default:
        return status;
    }
  }
  
  getHttpStatusCodeSeverity(record: UsageRecordOutputDto) {
    const code = record.upStatusCode || 200;
    if (code >= 200 && code < 300) return 'success';
    if (code >= 300 && code < 400) return 'info';
    if (code >= 400 && code < 500) return 'warn';
    return 'danger';
  }

  getTotalTokens(item: UsageRecordOutputDto) {
    return (item.inputTokens || 0) + (item.outputTokens || 0);
  }

  formatTokenCount = formatTokenCount;
  formatDuration = formatDuration;
}
