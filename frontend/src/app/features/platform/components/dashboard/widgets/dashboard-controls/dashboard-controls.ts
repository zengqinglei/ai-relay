import { isPlatformBrowser } from '@angular/common';
import { Component, output, input, signal, computed, effect, HostListener, inject, PLATFORM_ID } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectButtonModule } from 'primeng/selectbutton';

export interface TimeRange {
  startTime?: Date;
  endTime?: Date;
}

@Component({
  selector: 'app-dashboard-controls',
  standalone: true,
  imports: [FormsModule, SelectButtonModule, DatePickerModule],
  templateUrl: './dashboard-controls.html',
  host: {
    class: 'block'
  }
})
export class DashboardControls {
  readonly timeRangeChange = output<TimeRange>();
  readonly refreshIntervalChange = output<number>();
  readonly isRefreshing = input<boolean>(false);

  // Status Signals
  selectedTimeRange = signal<string>('today');
  selectedRefresh = signal<number>(30000);
  customDates = signal<Date[] | null>(null);

  // Screen Width tracking for Responsive SelectButtons
  screenWidth = signal<number>(1024);

  private baseTimeOptions = [
    { label: '今日', value: 'today', hideAt: 0 },
    { label: '7天', value: '7days', hideAt: 0 },
    { label: '30天', value: '30days', hideAt: 640 },
    { label: '自定义', value: 'custom', hideAt: 0 }
  ];

  private baseRefreshOptions = [
    { label: '关闭', value: 0, hideAt: 0 },
    { label: '10秒', value: 10000, hideAt: 768 },
    { label: '30秒', value: 30000, hideAt: 0 },
    { label: '60秒', value: 60000, hideAt: 640 }
  ];

  timeRangeOptions = computed(() => this.baseTimeOptions.filter(o => this.screenWidth() > o.hideAt));
  refreshOptions = computed(() => this.baseRefreshOptions.filter(o => this.screenWidth() > o.hideAt));

  private platformId = inject(PLATFORM_ID);

  constructor() {
    if (isPlatformBrowser(this.platformId)) {
      this.screenWidth.set(window.innerWidth);

      const savedRefresh = localStorage.getItem('dashboard_refresh_interval');
      if (savedRefresh) {
        this.selectedRefresh.set(Number(savedRefresh));
      }
    }

    // 1. 发射时间范围变更
    effect(() => {
      const range = this.selectedTimeRange();
      const dates = this.customDates();

      if (range === 'custom' && dates && dates.length === 2) {
        this.timeRangeChange.emit({ startTime: dates[0], endTime: dates[1] });
      } else if (range !== 'custom') {
        this.timeRangeChange.emit(this.calculateTimeRange(range));
      }
    });

    // 2. 监听屏幕变化，若当前项被隐藏则进行 fallback 取默认项
    effect(() => {
      const range = this.selectedTimeRange();
      const validTrs = this.timeRangeOptions().map(o => o.value);
      if (!validTrs.includes(range) && validTrs.length > 0) {
        setTimeout(() => this.onTimeRangeChange(validTrs[0]));
      }
    });

    // 3. 发射自动刷新间隔变更
    effect(() => {
      const currentRefresh = this.selectedRefresh();
      this.refreshIntervalChange.emit(currentRefresh);
    });

    // 4. 监听屏幕变化，若当前刷新选项被隐藏则进行 fallback
    effect(() => {
      const currentRefresh = this.selectedRefresh();
      const validRefs = this.refreshOptions().map(o => o.value);
      if (!validRefs.includes(currentRefresh) && validRefs.length > 0) {
        setTimeout(() => this.onRefreshChange(validRefs.includes(30000) ? 30000 : validRefs[0]));
      }
    });
  }

  @HostListener('window:resize')
  onResize() {
    this.screenWidth.set(window.innerWidth);
  }

  onTimeRangeChange(value: string | null) {
    if (value !== null && value !== undefined) {
      this.selectedTimeRange.set(value);
    }
  }

  onCustomDatesChange(value: Date[] | null) {
    this.customDates.set(value);
  }

  onRefreshChange(value: number | null) {
    if (value !== null && value !== undefined) {
      this.selectedRefresh.set(value);
      if (isPlatformBrowser(this.platformId)) {
        localStorage.setItem('dashboard_refresh_interval', value.toString());
      }
    }
  }

  private calculateTimeRange(range: string): TimeRange {
    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    switch (range) {
      case 'today':
        return { startTime: today, endTime: now };
      case '7days':
        return { startTime: new Date(today.getTime() - 7 * 24 * 60 * 60 * 1000), endTime: now };
      case '30days':
        return { startTime: new Date(today.getTime() - 30 * 24 * 60 * 60 * 1000), endTime: now };
      default:
        return {};
    }
  }
}
