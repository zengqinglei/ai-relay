import { Component, inject, signal, DestroyRef, ChangeDetectionStrategy, afterNextRender, OnInit } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { timer, fromEvent, of, EMPTY, Subject, merge, combineLatest } from 'rxjs';
import { switchMap, finalize, catchError, map, startWith, filter, distinctUntilChanged } from 'rxjs/operators';

import { ApiKeyTrendChartComponent } from './widgets/api-key-trend-chart/api-key-trend-chart';
import { DashboardControls, TimeRange } from './widgets/dashboard-controls/dashboard-controls';
import { MetricsCards } from './widgets/metrics-cards/metrics-cards';
import { ModelDistributionChartComponent } from './widgets/model-distribution-chart/model-distribution-chart';
import { UsageTrendChart } from './widgets/usage-trend-chart/usage-trend-chart';
import { LayoutService } from '../../../../layout/services/layout-service';
import { DashboardViewModel } from '../../models/dashboard.dto';
import { DashboardService } from '../../services/dashboard-service';

/**
 * Dashboard 主组件
 * 展示系统运行状态的核心指标和实时数据
 * 使用 OnPush 变更检测策略提升性能
 */
@Component({
  selector: 'app-workspace-dashboard',
  standalone: true,
  imports: [MetricsCards, UsageTrendChart, ModelDistributionChartComponent, ApiKeyTrendChartComponent, DashboardControls],
  templateUrl: './dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Dashboard implements OnInit {
  private readonly dashboardService = inject(DashboardService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly layoutService = inject(LayoutService);

  dashboardData = signal<DashboardViewModel | null>(null);
  isRefreshing = signal<boolean>(false);

  private readonly timeRange = signal<TimeRange>({});
  private readonly refreshInterval = signal<number>(30000);
  private readonly manualRefresh$ = new Subject<void>();
  private readonly intervalChange$ = new Subject<number>();
  private readonly autoTick$ = new Subject<void>();

  constructor() {
    // 立即订阅合并后的流，确保初始化时（effect触发）能捕获并拉取数据
    merge(this.manualRefresh$, this.autoTick$)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        switchMap(() => this.fetchData())
      )
      .subscribe();

    afterNextRender(() => {
      this.startPolling();
    });
  }

  ngOnInit() {
    this.layoutService.title.set('概览');
  }

  onTimeRangeChange(range: TimeRange) {
    this.timeRange.set(range);
    this.manualRefresh$.next();
  }

  onRefreshIntervalChange(interval: number) {
    this.refreshInterval.set(interval);
    this.intervalChange$.next(interval);
  }

  private startPolling(): void {
    const visibility$ = fromEvent(document, 'visibilitychange').pipe(
      startWith(document.visibilityState),
      map(() => document.visibilityState === 'visible'),
      distinctUntilChanged()
    );

    const interval$ = this.intervalChange$.pipe(startWith(this.refreshInterval()), distinctUntilChanged());

    combineLatest([visibility$, interval$])
      .pipe(
        switchMap(([visible, interval]) => {
          return visible && interval > 0 ? timer(0, interval) : EMPTY;
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.autoTick$.next());
  }

  private fetchData() {
    this.isRefreshing.set(true);
    const { startTime, endTime } = this.timeRange();
    return this.dashboardService.getDashboardData(startTime, endTime).pipe(
      catchError(err => {
        console.error('Dashboard error:', err);
        return of(null);
      }),
      finalize(() => this.isRefreshing.set(false)),
      filter((data): data is DashboardViewModel => data !== null),
      map(data => {
        this.dashboardData.set(data);
        return data;
      })
    );
  }
}
