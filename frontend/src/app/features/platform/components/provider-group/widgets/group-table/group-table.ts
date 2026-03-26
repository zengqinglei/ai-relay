import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, EventEmitter, inject, input, OnInit, Output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { RippleModule } from 'primeng/ripple';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';

import { PlatformIcon } from '../../../../../../shared/components/platform-icon/platform-icon';
import { PROVIDER_PLATFORM_OPTIONS } from '../../../../../../shared/constants/provider-platform.constants';
import { PlatformLabelPipe } from '../../../../../../shared/pipes/platform-label-pipe';
import { SchedulingStrategyLabelPipe } from '../../../../../../shared/pipes/scheduling-strategy-label-pipe';
import { SchedulingStrategySeverityPipe } from '../../../../../../shared/pipes/scheduling-strategy-severity-pipe';
import { FilterStateService } from '../../../../../../shared/services/filter-state.service';
import {
  ProviderGroupOutputDto,
  GroupSchedulingStrategy,
  ProviderGroupAccountRelationDto,
  SCHEDULING_STRATEGY_DESCRIPTIONS
} from '../../../../models/provider-group.dto';

export interface GroupTableFilterEvent {
  offset: number;
  limit: number;
  q?: string;
  platform?: string;
  sorting?: string;
}

@Component({
  selector: 'app-group-table',
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
    IconFieldModule,
    InputIconModule,
    RippleModule,
    PlatformLabelPipe,
    SchedulingStrategyLabelPipe,
    SchedulingStrategySeverityPipe,
    PlatformIcon
  ],
  templateUrl: './group-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GroupTable implements OnInit {
  groups = input.required<ProviderGroupOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly filterChange = new EventEmitter<GroupTableFilterEvent>();
  @Output() readonly add = new EventEmitter<void>();
  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();

  // Filter states
  searchQuery = signal('');
  selectedPlatform = signal<string | null>(null);

  // Dropdown options
  platformOptions = PROVIDER_PLATFORM_OPTIONS.map(o => ({ label: o.label, value: o.value }));

  GroupSchedulingStrategy = GroupSchedulingStrategy;

  // Pagination state
  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);

  private destroyRef = inject(DestroyRef);
  private filterStateService = inject(FilterStateService);
  private searchSubject = new Subject<string>();

  private readonly FILTER_KEY = 'provider-group';

  constructor() {
    this.searchSubject.pipe(
      debounceTime(300),
      distinctUntilChanged(),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => this.onFilter());
  }

  ngOnInit() {
    const saved = this.filterStateService.load<{ keyword: string; platform: string | null }>(this.FILTER_KEY);
    if (saved.keyword) this.searchQuery.set(saved.keyword);
    if (saved.platform !== undefined) this.selectedPlatform.set(saved.platform ?? null);
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
  }

  onSelectChange() {
    this.onFilter();
  }

  onFilter() {
    this.first = 0; // Reset to first page
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

  private emitFilterChange() {
    this.filterStateService.save(this.FILTER_KEY, {
      keyword: this.searchQuery(),
      platform: this.selectedPlatform()
    });
    this.filterChange.emit({
      offset: this.first,
      limit: this.rows,
      q: this.searchQuery(),
      platform: this.selectedPlatform() ?? undefined,
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    });
  }

  showWeight(group: ProviderGroupOutputDto): boolean {
    return group.schedulingStrategy === GroupSchedulingStrategy.WeightedRandom;
  }

  showPriority(group: ProviderGroupOutputDto): boolean {
    return group.schedulingStrategy === GroupSchedulingStrategy.Priority;
  }

  getAccountValidity(account: ProviderGroupAccountRelationDto): string {
    if (!account.expiresIn || !account.tokenObtainedTime) return '永久有效';

    const obtained = new Date(account.tokenObtainedTime);
    const expiresAt = new Date(obtained.getTime() + account.expiresIn * 1000);

    // Check if expired
    if (expiresAt < new Date()) {
      return '已过期';
    }

    return expiresAt.toLocaleString();
  }

  /**
   * 获取调度策略详细说明
   */
  getStrategyDescription(strategy: GroupSchedulingStrategy): string {
    return SCHEDULING_STRATEGY_DESCRIPTIONS[strategy] || '';
  }
}
