import { isPlatformBrowser } from '@angular/common';
import {
  Component,
  Input,
  OnChanges,
  SimpleChanges,
  effect,
  signal,
  ChangeDetectionStrategy,
  inject,
  OnInit,
  PLATFORM_ID,
  input
} from '@angular/core';
import { ChartModule } from 'primeng/chart';

import { LayoutService } from '../../../../../../layout/services/layout-service';
import { ApiKeyTrendOutputDto } from '../../../../models/usage.dto';

const increaseLegendSpacing = {
  id: 'increaseLegendSpacing',
  beforeInit(chart: any) {
    const originalFit = chart.legend.fit;
    chart.legend.fit = function fit() {
      originalFit.bind(chart.legend)();
      this.height += 10;
    };
  }
};

@Component({
  selector: 'app-api-key-trend-chart',
  imports: [ChartModule],
  templateUrl: './api-key-trend-chart.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApiKeyTrendChartComponent implements OnChanges, OnInit {
  @Input() data: ApiKeyTrendOutputDto[] = [];
  title = input('热门 API KEY 调用趋势');
  description = input('所选时间范围内调用量 TOP 7 的 API KEY');

  private readonly platformId = inject(PLATFORM_ID);
  private readonly layoutService = inject(LayoutService);

  chartData = signal<any>(null);
  chartOptions = signal<any>(null);
  chartPlugins = [increaseLegendSpacing];

  constructor() {
    effect(() => {
      const config = this.layoutService.layoutConfig();
      this.title();
      this.description();

      if (isPlatformBrowser(this.platformId)) {
        requestAnimationFrame(() => {
          this.updateChartOptions();
          if (this.data && this.data.length > 0) {
            this.updateChartData();
          }
        });
      }
    });
  }

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.updateChartOptions();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['data'] && this.data && this.data.length > 0) {
      if (isPlatformBrowser(this.platformId)) {
        this.updateChartData();
      }
    }
  }

  private updateChartData(): void {
    const documentStyle = getComputedStyle(document.documentElement);
    const colors = [
      documentStyle.getPropertyValue('--p-blue-500'),
      documentStyle.getPropertyValue('--p-green-500'),
      documentStyle.getPropertyValue('--p-yellow-500'),
      documentStyle.getPropertyValue('--p-cyan-500'),
      documentStyle.getPropertyValue('--p-pink-500'),
      documentStyle.getPropertyValue('--p-indigo-500'),
      documentStyle.getPropertyValue('--p-teal-500'),
      documentStyle.getPropertyValue('--p-orange-500'),
      documentStyle.getPropertyValue('--p-purple-500'),
      documentStyle.getPropertyValue('--p-red-500')
    ];

    const rawData = this.data[0]?.trend || [];
    let isDaily = false;
    if (rawData.length > 1) {
      const diffHours = (new Date(rawData[rawData.length - 1].time).getTime() - new Date(rawData[0].time).getTime()) / (1000 * 60 * 60);
      isDaily = diffHours > 72;
    }

    const labels = rawData.map(d => {
      const date = new Date(d.time);
      if (isNaN(date.getTime())) {
        return d.time;
      }
      if (isDaily) {
        return `${(date.getMonth() + 1).toString().padStart(2, '0')}-${date.getDate().toString().padStart(2, '0')}`;
      } else {
        return `${date.getHours().toString().padStart(2, '0')}:${date.getMinutes().toString().padStart(2, '0')}`;
      }
    });

    const datasets = this.data.map((apiKey, index) => ({
      label: `${apiKey.apiKeyName} (${this.formatNumber(apiKey.totalRequests)})`,
      data: apiKey.trend.map(t => t.requests),
      borderColor: colors[index % colors.length],
      backgroundColor: this.hexToRgba(colors[index % colors.length], 0.1),
      tension: 0.4,
      fill: false,
      pointRadius: 0,
      pointHoverRadius: 4
    }));

    this.chartData.set({
      labels,
      datasets
    });
  }

  private updateChartOptions(): void {
    const documentStyle = getComputedStyle(document.documentElement);
    const textColor = documentStyle.getPropertyValue('--p-text-color');
    const textColorSecondary = documentStyle.getPropertyValue('--p-text-muted-color');
    const surfaceBorder = documentStyle.getPropertyValue('--p-content-border-color');

    this.chartOptions.set({
      responsive: true,
      maintainAspectRatio: false,
      layout: {
        padding: {
          top: 10,
          bottom: 10
        }
      },
      interaction: {
        mode: 'index',
        intersect: false
      },
      plugins: {
        legend: {
          position: 'top',
          align: 'center',
          labels: {
            color: textColor,
            usePointStyle: true,
            padding: 20,
            pointStyle: 'circle',
            boxWidth: 10,
            boxHeight: 10,
            font: {
              family: documentStyle.getPropertyValue('--font-family')
            },
            generateLabels: (chart: any) => {
              const datasets = chart.data.datasets;
              return datasets.map((dataset: any, i: number) => {
                const meta = chart.getDatasetMeta(i);
                const hidden = meta.hidden;
                const styleColor = hidden ? textColorSecondary || '#888' : dataset.borderColor;
                const fontColor = hidden ? textColorSecondary || '#888' : textColor;

                return {
                  text: dataset.label,
                  fillStyle: styleColor,
                  strokeStyle: styleColor,
                  lineWidth: 0,
                  hidden: !chart.isDatasetVisible(i),
                  datasetIndex: i,
                  fontColor: fontColor,
                  pointStyle: 'circle'
                };
              });
            }
          }
        },
        tooltip: {
          mode: 'index',
          intersect: false,
          backgroundColor: documentStyle.getPropertyValue('--p-content-background'),
          titleColor: textColor,
          bodyColor: textColor,
          borderColor: surfaceBorder,
          borderWidth: 1,
          usePointStyle: true,
          boxWidth: 10,
          boxHeight: 10,
          boxPadding: 4,
          callbacks: {
            labelColor: (context: any) => {
              return {
                borderColor: context.dataset.borderColor,
                backgroundColor: context.dataset.borderColor,
                borderWidth: 0,
                borderRadius: 4
              };
            },
            label: (context: any) => {
              return `${context.dataset.label.split(' (')[0]}: ${context.parsed.y} 请求`;
            }
          }
        }
      },
      scales: {
        x: {
          ticks: {
            color: textColorSecondary,
            maxRotation: 0,
            autoSkip: true,
            maxTicksLimit: 12
          },
          grid: {
            display: false
          }
        },
        y: {
          ticks: {
            color: textColorSecondary
          },
          grid: {
            color: surfaceBorder,
            drawBorder: false
          },
          beginAtZero: true
        }
      }
    });
  }

  private formatNumber(num: number): string {
    return new Intl.NumberFormat('zh-CN').format(num);
  }

  private hexToRgba(hex: string, alpha: number): string {
    if (!hex) return `rgba(0, 0, 0, ${alpha})`;
    hex = hex.trim();

    if (hex.startsWith('rgb')) {
      return hex.replace(')', `, ${alpha})`).replace('rgb', 'rgba');
    }

    if (hex.startsWith('#')) {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    return hex;
  }
}
