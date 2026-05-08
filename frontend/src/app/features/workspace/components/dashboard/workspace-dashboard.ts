import { CommonModule } from '@angular/common';
import { ChangeDetectionStrategy, Component, DestroyRef, afterNextRender, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { EMPTY, Subject, combineLatest, forkJoin, fromEvent, merge, of, timer } from 'rxjs';
import { catchError, distinctUntilChanged, exhaustMap, filter, finalize, map, startWith, switchMap } from 'rxjs/operators';

import { LayoutService } from '../../../../layout/services/layout-service';
import { formatDuration, formatTokenCount } from '../../../../shared/utils/format.utils';
import { DashboardControls, TimeRange } from '../../../platform/components/dashboard/widgets/dashboard-controls/dashboard-controls';
import { ApiKeyTrendChartComponent } from '../../../platform/components/dashboard/widgets/api-key-trend-chart/api-key-trend-chart';
import { ModelDistributionChartComponent } from '../../../platform/components/dashboard/widgets/model-distribution-chart/model-distribution-chart';
import { UsageTrendChart } from '../../../platform/components/dashboard/widgets/usage-trend-chart/usage-trend-chart';
import { DashboardViewModel } from '../../../platform/models/dashboard.dto';
import { UsageRecordOutputDto } from '../../../platform/models/usage.dto';
import { DashboardService } from '../../../platform/services/dashboard-service';
import { UsageRecordService } from '../../../platform/services/usage-record-service';
import { ChatSessionService } from '../../services/chat-session-service';

interface WorkspaceDashboardMetric {
  label: string;
  value: string;
  hint: string;
  accentClass: string;
  iconClass: string;
  route: string;
}

@Component({
  selector: 'app-workspace-dashboard-page',
  standalone: true,
  imports: [
    CommonModule,
    ButtonModule,
    CardModule,
    TagModule,
    TooltipModule,
    DashboardControls,
    UsageTrendChart,
    ModelDistributionChartComponent,
    ApiKeyTrendChartComponent
  ],
  templateUrl: './workspace-dashboard.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class WorkspaceDashboardPage {
  private readonly layoutService = inject(LayoutService);
  private readonly dashboardService = inject(DashboardService);
  private readonly usageRecordService = inject(UsageRecordService);
  private readonly chatSessionService = inject(ChatSessionService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  readonly dashboardData = signal<DashboardViewModel | null>(null);
  readonly recentLogs = signal<UsageRecordOutputDto[]>([]);
  readonly totalSessions = signal(0);
  readonly isRefreshing = signal(false);

  private readonly timeRange = signal<TimeRange>(this.createTodayRange());
  private readonly refreshInterval = signal(30000);
  private readonly manualRefresh$ = new Subject<void>();
  private readonly intervalChange$ = new Subject<number>();
  private readonly autoTick$ = new Subject<void>();

  readonly metrics = computed<WorkspaceDashboardMetric[]>(() => {
    const dashboard = this.dashboardData();
    if (!dashboard) {
      return [];
    }

    const totalTokens = dashboard.usage.totalInputTokens + dashboard.usage.totalOutputTokens;

    return [
      {
        label: '区间请求数',
        value: dashboard.usage.totalRequests.toLocaleString('zh-CN'),
        hint: `${dashboard.usage.totalRequests > 0 ? Math.round((dashboard.usage.successRequests / dashboard.usage.totalRequests) * 100) : 0}% 成功率`,
        accentClass: 'bg-primary-500/10 text-primary-600 dark:text-primary-500',
        iconClass: 'pi pi-bolt',
        route: '/workspace/usage-logs'
      },
      {
        label: '区间 Token',
        value: formatTokenCount(totalTokens),
        hint: `输入 ${formatTokenCount(dashboard.usage.totalInputTokens)} / 输出 ${formatTokenCount(dashboard.usage.totalOutputTokens)}`,
        accentClass: 'bg-emerald-500/10 text-emerald-600 dark:text-emerald-500',
        iconClass: 'pi pi-database',
        route: '/workspace/usage-logs'
      },
      {
        label: '有效订阅',
        value: `${dashboard.subscriptions.activeSubscriptions}`,
        hint: `${dashboard.subscriptions.totalSubscriptions} 个订阅，${dashboard.subscriptions.expiringSoon} 个 7 天内到期`,
        accentClass: 'bg-indigo-500/10 text-indigo-600 dark:text-indigo-500',
        iconClass: 'pi pi-key',
        route: '/workspace/my-subscriptions'
      },
      {
        label: '区间费用',
        value: `$${dashboard.usage.totalCost.toFixed(4)}`,
        hint: `${this.totalSessions()} 个聊天会话可追溯`,
        accentClass: 'bg-amber-500/10 text-amber-600 dark:text-amber-500',
        iconClass: 'pi pi-wallet',
        route: '/workspace/chat'
      }
    ];
  });

  constructor() {
    this.layoutService.title.set('工作区仪表盘');

    merge(this.manualRefresh$, this.autoTick$)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        exhaustMap(() => this.fetchDashboardData())
      )
      .subscribe();

    afterNextRender(() => {
      this.startPolling();
    });
  }

  onTimeRangeChange(range: TimeRange) {
    this.timeRange.set(range);
    this.manualRefresh$.next();
  }

  onRefreshIntervalChange(interval: number) {
    this.refreshInterval.set(interval);
    this.intervalChange$.next(interval);
  }

  navigateTo(path: string) {
    this.router.navigate([path]);
  }

  formatTokens(value: number) {
    return formatTokenCount(value);
  }

  formatDuration(value?: number | null) {
    return formatDuration(value);
  }

  getStatusSeverity(status: string): 'success' | 'danger' | 'info' {
    switch (status) {
      case 'Success':
        return 'success';
      case 'Failed':
        return 'danger';
      default:
        return 'info';
    }
  }

  getStatusLabel(status: string) {
    switch (status) {
      case 'Success':
        return '成功';
      case 'Failed':
        return '失败';
      case 'InProgress':
        return '进行中';
      default:
        return status || '未知';
    }
  }

  private startPolling() {
    const visibility$ = fromEvent(document, 'visibilitychange').pipe(
      startWith(document.visibilityState),
      map(() => document.visibilityState === 'visible'),
      distinctUntilChanged()
    );

    const interval$ = this.intervalChange$.pipe(startWith(this.refreshInterval()), distinctUntilChanged());

    combineLatest([visibility$, interval$])
      .pipe(
        switchMap(([visible, interval]) => (visible && interval > 0 ? timer(interval, interval) : EMPTY)),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe(() => this.autoTick$.next());
  }

  private fetchDashboardData() {
    this.isRefreshing.set(true);
    const { startTime, endTime } = this.timeRange();

    return forkJoin({
      dashboard: this.dashboardService.getDashboardData(startTime, endTime),
      usageRecords: this.usageRecordService.getUsageRecords({
        offset: 0,
        limit: 8,
        sorting: 'creationTime desc',
        startTime: startTime?.toISOString(),
        endTime: endTime?.toISOString()
      }),
      sessions: this.chatSessionService.getSessions()
    }).pipe(
      catchError(error => {
        console.error('Workspace dashboard error:', error);
        return of(null);
      }),
      finalize(() => this.isRefreshing.set(false)),
      filter((data): data is NonNullable<typeof data> => data !== null),
      map(({ dashboard, usageRecords, sessions }) => {
        this.dashboardData.set(dashboard);
        this.recentLogs.set(usageRecords.items);
        this.totalSessions.set(sessions.length);
        return dashboard;
      })
    );
  }

  private createTodayRange(): TimeRange {
    const now = new Date();
    return {
      startTime: new Date(now.getFullYear(), now.getMonth(), now.getDate()),
      endTime: now
    };
  }
}
