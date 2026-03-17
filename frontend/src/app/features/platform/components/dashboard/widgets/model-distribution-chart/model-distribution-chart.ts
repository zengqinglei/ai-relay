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
  PLATFORM_ID
} from '@angular/core';
import { ChartModule } from 'primeng/chart';

import { LayoutService } from '../../../../../../layout/services/layout-service';
import { ModelDistributionOutputDto } from '../../../../models/usage.dto';

@Component({
  selector: 'app-model-distribution-chart',
  imports: [ChartModule],
  templateUrl: './model-distribution-chart.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ModelDistributionChartComponent implements OnChanges, OnInit {
  @Input() data: ModelDistributionOutputDto[] = [];

  private readonly platformId = inject(PLATFORM_ID);
  private readonly layoutService = inject(LayoutService);

  chartData = signal<any>(null);
  chartOptions = signal<any>(null);

  constructor() {
    // 监听数据变化和主题配置变化
    effect(() => {
      // 订阅布局配置，确保主题切换时触发副作用
      const config = this.layoutService.layoutConfig();

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
    // ngOnChanges 主要处理 input 数据变化，effect 处理主题变化
    // 为了避免冲突，这里可以只处理初始数据加载
    if (changes['data'] && this.data) {
      if (isPlatformBrowser(this.platformId)) {
        this.updateChartData();
      }
    }
  }

  private updateChartData(): void {
    const documentStyle = getComputedStyle(document.documentElement);
    // 定义一组对比度较高的颜色
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

    this.chartData.set({
      labels: this.data.map(item => item.model),
      datasets: [
        {
          data: this.data.map(item => item.requestCount),
          backgroundColor: colors.slice(0, this.data.length),
          hoverBackgroundColor: colors.slice(0, this.data.length).map(c => this.adjustBrightness(c, -20)), // 稍微变暗
          borderWidth: 0
        }
      ]
    });
  }

  private updateChartOptions(): void {
    const documentStyle = getComputedStyle(document.documentElement);
    const textColor = documentStyle.getPropertyValue('--p-text-color');
    const surfaceBorder = documentStyle.getPropertyValue('--p-content-border-color');

    this.chartOptions.set({
      responsive: true,
      maintainAspectRatio: false,
      layout: {
        padding: {
          left: 10,
          right: 80, // 增加右侧间距，使图例看起来向左移动了一点（实际上是图表向左压缩了一点，给图例留出空间）
          top: 10,
          bottom: 10
        }
      },
      plugins: {
        legend: {
          position: 'right',
          labels: {
            color: textColor,
            usePointStyle: true,
            padding: 20,
            pointStyle: 'circle',
            boxWidth: 10,
            boxHeight: 10,
            font: {
              size: 14
            },
            generateLabels: (chart: any) => {
              const data = chart.data;
              if (data.labels.length && data.datasets.length) {
                return data.labels.map((label: string, i: number) => {
                  const meta = chart.getDatasetMeta(0);
                  const style = meta.controller.getStyle(i);
                  const hidden = meta.data[i].hidden;

                  // 未选中状态颜色处理
                  const styleColor = hidden ? documentStyle.getPropertyValue('--p-text-muted-color') || '#888' : style.backgroundColor;
                  const fontColor = hidden ? documentStyle.getPropertyValue('--p-text-muted-color') || '#888' : textColor;

                  return {
                    text: label,
                    fillStyle: styleColor,
                    strokeStyle: styleColor,
                    lineWidth: 0,
                    hidden: hidden,
                    index: i,
                    fontColor: fontColor,
                    pointStyle: 'circle'
                  };
                });
              }
              return [];
            }
          },
          onClick: (e: any, legendItem: any, legend: any) => {
            const index = legendItem.index;
            const ci = legend.chart;
            const meta = ci.getDatasetMeta(0);

            // Toggle hidden state
            meta.data[index].hidden = !meta.data[index].hidden;

            ci.update();
          }
        },
        tooltip: {
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
              // Doughnut/Pie chart dataset structure is different (one dataset, multiple data points)
              // context.dataset.backgroundColor is an array
              const bgColors = context.dataset.backgroundColor as string[];
              const color = bgColors[context.dataIndex];
              return {
                borderColor: color,
                backgroundColor: color,
                borderWidth: 0,
                borderRadius: 4
              };
            },
            label: (context: any) => {
              const item = this.data[context.dataIndex];
              return [
                `${context.label}: ${context.parsed}`,
                `占比: ${item.percentage.toFixed(2)}%`,
                `TOKEN: ${this.formatNumber(item.totalTokens)}`,
                `成本: ¥${item.totalCost.toFixed(4)}`
              ];
            }
          }
        }
      },
      cutout: '60%',
      radius: '90%'
    });
  }

  private formatNumber(num: number): string {
    return new Intl.NumberFormat('zh-CN').format(num);
  }

  // 辅助函数：调整颜色亮度 (用于hover效果)
  private adjustBrightness(color: string, amount: number) {
    // 简单处理，如果是 CSS 变量引用的颜色，可能不生效，但不会报错
    if (!color || color.startsWith('var')) return color;
    return `#${color
      .replace(/^#/, '')
      .replace(/../g, color => `0${Math.min(255, Math.max(0, parseInt(color, 16) + amount)).toString(16)}`.substr(-2))}`;
  }
}
