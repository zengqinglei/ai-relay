import { CommonModule } from '@angular/common';
import { AfterViewInit, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, EventEmitter, inject, input, OnInit, Output, signal, ViewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { Popover, PopoverModule } from 'primeng/popover';
import { RippleModule } from 'primeng/ripple';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

import { getProviderAuthLabel } from '../../../../../../shared/constants/provider.constants';
import { ROUTE_PROFILE_FULL_LABELS, ROUTE_PROFILE_LABELS } from '../../../../../../shared/constants/route-profile.constants';
import { RouteProfile } from '../../../../../../shared/models/route-profile.enum';
import { SchedulingStrategyLabelPipe } from '../../../../../../shared/pipes/scheduling-strategy-label-pipe';
import { SchedulingStrategySeverityPipe } from '../../../../../../shared/pipes/scheduling-strategy-severity-pipe';
import { FilterStateService } from '../../../../../../shared/services/filter-state.service';
import {
  GroupSchedulingStrategy,
  GroupAccountRelationOutputDto,
  ProviderGroupOutputDto,
  SCHEDULING_STRATEGY_DESCRIPTIONS
} from '../../../../models/provider-group.dto';
import {
  RelationPopoverContentComponent,
  RelationPopoverItem
} from '../../../shared/widgets/relation-popover-content/relation-popover-content';

export interface GroupTableFilterEvent {
  offset: number;
  limit: number;
  q?: string;
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
    TagModule,
    TooltipModule,
    IconFieldModule,
    InputIconModule,
    RippleModule,
    PopoverModule,
    RelationPopoverContentComponent,
    SchedulingStrategyLabelPipe,
    SchedulingStrategySeverityPipe
  ],
  templateUrl: './group-table.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class GroupTable implements OnInit, AfterViewInit {
  groups = input.required<ProviderGroupOutputDto[]>();
  totalRecords = input.required<number>();
  loading = input<boolean>(false);

  @Output() readonly filterChange = new EventEmitter<GroupTableFilterEvent>();
  @Output() readonly add = new EventEmitter<void>();
  @Output() readonly edit = new EventEmitter<string>();
  @Output() readonly delete = new EventEmitter<string>();

  searchQuery = signal('');
  GroupSchedulingStrategy = GroupSchedulingStrategy;
  first = 0;
  rows = 10;
  sortField = signal<string>('creationTime');
  sortOrder = signal<number>(-1);
  activeRouteProfiles = signal<RouteProfile[]>([]);
  visibleRouteCount = signal(3);

  @ViewChild('routeColumnContainer') routeColumnContainer?: ElementRef<HTMLElement>;

  private readonly destroyRef = inject(DestroyRef);
  private readonly filterStateService = inject(FilterStateService);
  private readonly searchSubject = new Subject<string>();
  private readonly FILTER_KEY = 'provider-group';
  private routeResizeObserver?: ResizeObserver;

  constructor() {
    this.searchSubject
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.onFilter());
  }

  ngOnInit() {
    const saved = this.filterStateService.load<{ keyword: string }>(this.FILTER_KEY);
    if (saved.keyword) this.searchQuery.set(saved.keyword);
  }

  ngAfterViewInit(): void {
    queueMicrotask(() => this.setupRouteResizeObserver());
  }

  private setupRouteResizeObserver(): void {
    const element = this.routeColumnContainer?.nativeElement;
    if (!element || typeof ResizeObserver === 'undefined') {
      return;
    }

    this.routeResizeObserver?.disconnect();
    this.routeResizeObserver = new ResizeObserver(entries => {
      const width = entries[0]?.contentRect.width ?? element.clientWidth;
      this.updateVisibleRouteCount(width);
    });
    this.routeResizeObserver.observe(element);
    this.destroyRef.onDestroy(() => this.routeResizeObserver?.disconnect());
  }

  private updateVisibleRouteCount(width: number): void {
    // 根据宽度动态计算可容纳的标签数 (每个标签约 60-80px)
    if (width >= 280) {
      this.visibleRouteCount.set(4);
    } else if (width >= 200) {
      this.visibleRouteCount.set(3);
    } else if (width >= 120) {
      this.visibleRouteCount.set(2);
    } else {
      this.visibleRouteCount.set(1);
    }
  }

  onSearchQueryChange(value: string) {
    this.searchQuery.set(value);
    this.searchSubject.next(value);
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

  private emitFilterChange() {
    this.filterStateService.save(this.FILTER_KEY, {
      keyword: this.searchQuery()
    });
    this.filterChange.emit({
      offset: this.first,
      limit: this.rows,
      q: this.searchQuery(),
      sorting: `${this.sortField()} ${this.sortOrder() === 1 ? 'asc' : 'desc'}`
    });
  }

  showWeight(group: ProviderGroupOutputDto): boolean {
    return group.schedulingStrategy === GroupSchedulingStrategy.WeightedRandom;
  }

  showPriority(group: ProviderGroupOutputDto): boolean {
    return group.schedulingStrategy === GroupSchedulingStrategy.Priority;
  }

  getAccountExpiryState(account: GroupAccountRelationOutputDto): 'expired' | 'warning' | 'ok' | 'forever' {
    if (!account.expiresAt) return 'forever';
    // 借鉴 AccountTable 逻辑：如果账户已禁用，不触发过期警告状态（降级显示）
    if (account.isActive === false) return 'forever';

    const expiry = new Date(account.expiresAt).getTime();
    if (isNaN(expiry)) return 'forever';

    const now = Date.now();
    const diff = expiry - now;

    if (diff <= 0) return 'expired';
    if (diff < 3 * 24 * 3600 * 1000) return 'warning'; // 3天内预警
    return 'ok';
  }

  getAccountValidity(account: GroupAccountRelationOutputDto): string {
    if (!account.expiresAt) return '永久有效';
    return new Date(account.expiresAt).toLocaleString();
  }

  getStrategyDescription(strategy: GroupSchedulingStrategy): string {
    return SCHEDULING_STRATEGY_DESCRIPTIONS[strategy] || '';
  }

  toggleRouteProfilesPopover(event: Event, popover: Popover, profiles: RouteProfile[]) {
    this.activeRouteProfiles.set(profiles);
    popover.toggle(event);
  }

  getRoutePopoverItems(profiles: RouteProfile[]): RelationPopoverItem[] {
    return profiles.map(profile => ({
      id: profile,
      leftText: this.getRouteProfileLabel(profile),
      rightText: this.getRouteProfilePath(profile)
    }));
  }

  getProviderAuthLabel(
    provider: GroupAccountRelationOutputDto['provider'],
    authMethod: GroupAccountRelationOutputDto['authMethod']
  ): string {
    return getProviderAuthLabel(provider, authMethod);
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
}
