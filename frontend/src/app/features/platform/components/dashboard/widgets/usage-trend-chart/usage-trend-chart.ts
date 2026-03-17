import { isPlatformBrowser } from '@angular/common';
import { Component, input, inject, PLATFORM_ID, ChangeDetectionStrategy, signal, effect, OnInit } from '@angular/core';
import { CardModule } from 'primeng/card';
import { ChartModule } from 'primeng/chart';

import { LayoutService } from '../../../../../../layout/services/layout-service';
import { UsageTrendOutputDto } from '../../../../models/usage.dto';

// Chart.js 插件：增加图例与图表之间的间距
const increaseLegendSpacing = {
  id: 'increaseLegendSpacing',
  beforeInit(chart: any) {
    const originalFit = chart.legend.fit;
    chart.legend.fit = function fit() {
      originalFit.bind(chart.legend)();
      this.height += 10; // 增加10px高度（减少间距）
    };
  }
};

/**
 * 流量趋势图表组件
 * 展示 API 请求流量的时间序列数据
 * 响应 LayoutService 的配置变化来适配主题
 */
@Component({
  selector: 'app-usage-trend-chart',
  standalone: true,
  imports: [ChartModule, CardModule],
  templateUrl: './usage-trend-chart.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class UsageTrendChart implements OnInit {
  trendData = input.required<UsageTrendOutputDto[]>();

  private readonly platformId = inject(PLATFORM_ID);
  private readonly layoutService = inject(LayoutService);

  chartData = signal<any>({ labels: [], datasets: [] });
  chartOptions = signal<any>({});
  chartPlugins = [increaseLegendSpacing];

  constructor() {
    // 监听数据变化和主题配置变化
    effect(() => {
      const data = this.trendData();
      // 订阅布局配置，确保主题切换时触发副作用
      const config = this.layoutService.layoutConfig();

      if (isPlatformBrowser(this.platformId)) {
        // 使用 requestAnimationFrame 确保在样式计算应用后执行
        requestAnimationFrame(() => {
          this.initChartOptions();
          if (data && data.length > 0) {
            this.updateChartData();
          }
        });
      }
    });
  }

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      this.initChartOptions();
    }
  }

  /**
   * 更新图表数据
   */
  private updateChartData(): void {
    const data = this.trendData();
    const documentStyle = getComputedStyle(document.documentElement);

    // 使用 PrimeNG 语义化 CSS 变量
    const blueColor = documentStyle.getPropertyValue('--p-blue-500');
    const greenColor = documentStyle.getPropertyValue('--p-green-500');
    const orangeColor = documentStyle.getPropertyValue('--p-orange-500');

    this.chartData.set({
      labels: data.map(d => d.time),
      datasets: [
        {
          label: '请求数',
          data: data.map(d => d.requests),
          borderColor: blueColor,
          backgroundColor: this.hexToRgba(blueColor, 0.1),
          borderWidth: 2,
          fill: true, // 填充背景色
          tension: 0.4,
          pointRadius: 0,
          pointHoverRadius: 4,
          yAxisID: 'y'
        },
        {
          label: '输入 TOKEN',
          data: data.map(d => d.inputTokens),
          borderColor: greenColor,
          backgroundColor: this.hexToRgba(greenColor, 0.1),
          borderWidth: 2,
          fill: true,
          tension: 0.4,
          pointRadius: 0,
          pointHoverRadius: 4,
          yAxisID: 'y1'
        },
        {
          label: '输出 TOKEN',
          data: data.map(d => d.outputTokens),
          borderColor: orangeColor,
          backgroundColor: this.hexToRgba(orangeColor, 0.1),
          borderWidth: 2,
          fill: true,
          tension: 0.4,
          pointRadius: 0,
          pointHoverRadius: 4,
          yAxisID: 'y1'
        }
      ]
    });
  }

  /**
   * 初始化图表配置
   */
  private initChartOptions(): void {
    const documentStyle = getComputedStyle(document.documentElement);
    const textColor = documentStyle.getPropertyValue('--p-text-color');
    const textColorSecondary = documentStyle.getPropertyValue('--p-text-muted-color');
    const surfaceBorder = documentStyle.getPropertyValue('--p-content-border-color');

    // 定义颜色，确保它们在暗色模式下可见
    const gridColor = surfaceBorder || 'rgba(255,255,255,0.1)';
    const tickColor = textColorSecondary || '#888';

    this.chartOptions.set({
      responsive: true,
      maintainAspectRatio: false,
      layout: {
        padding: {
          top: 10, // 增加顶部间距，替代之前的插件
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

                // 未选中状态颜色处理
                const fillStyle = hidden ? textColorSecondary || '#888' : dataset.borderColor;
                const strokeStyle = hidden ? textColorSecondary || '#888' : dataset.borderColor;
                const fontColor = hidden ? textColorSecondary || '#888' : textColor;

                return {
                  text: dataset.label,
                  fillStyle: fillStyle,
                  strokeStyle: strokeStyle,
                  lineWidth: 0,
                  hidden: isNaN(dataset.data[0]) || meta.hidden,
                  index: i,
                  datasetIndex: i,
                  fontColor: fontColor,
                  pointStyle: 'circle',
                  _fillStyle: fillStyle,
                  _strokeStyle: strokeStyle
                };
              });
            }
          }
          // 使用默认的图例点击行为
        },
        tooltip: {
          mode: 'index',
          intersect: false,
          backgroundColor: documentStyle.getPropertyValue('--p-content-background'),
          titleColor: textColor,
          bodyColor: textColor,
          borderColor: surfaceBorder,
          borderWidth: 1,
          usePointStyle: true, // 使用点样式
          boxWidth: 10, // 设置点大小
          boxHeight: 10,
          boxPadding: 4, // 增加点与文字间距
          callbacks: {
            labelColor: (context: any) => {
              return {
                borderColor: context.dataset.borderColor,
                backgroundColor: context.dataset.borderColor, // 使用 borderColor 作为背景，实现实心圆
                borderWidth: 0,
                borderRadius: 4 // 圆形
              };
            },
            label: (context: any) => {
              let label = context.dataset.label || '';
              if (label) {
                label += ': ';
              }
              if (context.parsed.y !== null) {
                if (context.dataset.yAxisID === 'y1') {
                  label += this.formatTokenCount(context.parsed.y);
                } else {
                  label += context.parsed.y;
                }
              }
              return label;
            }
          }
        }
      },
      scales: {
        x: {
          grid: { display: false },
          ticks: {
            color: tickColor,
            maxRotation: 0,
            autoSkip: true,
            maxTicksLimit: 12
          }
        },
        y: {
          type: 'linear',
          display: true,
          position: 'left',
          grid: {
            display: true,
            color: gridColor,
            drawBorder: false
          },
          ticks: {
            color: tickColor
          },
          title: {
            display: true,
            text: '请求数',
            color: tickColor
          }
        },
        y1: {
          type: 'linear',
          display: true,
          position: 'right',
          grid: {
            drawOnChartArea: false
          },
          ticks: {
            color: tickColor,
            callback: (value: number) => this.formatTokenCount(value)
          },
          title: {
            display: true,
            text: 'TOKEN 数',
            color: tickColor
          }
        }
      }
    });
  }

  // 格式化 Token 数量（K, M, B）
  private formatTokenCount(num: number): string {
    if (num >= 1_000_000_000) return `${(num / 1_000_000_000).toFixed(1)}B`;
    if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
    if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`;
    return num.toLocaleString('zh-CN');
  }

  // 辅助函数：Hex 转 RGBA
  private hexToRgba(hex: string, alpha: number): string {
    if (!hex) return `rgba(0, 0, 0, ${alpha})`;
    // 处理 CSS 变量返回的颜色可能包含空格
    hex = hex.trim();

    // 如果已经是 rgba 或 rgb
    if (hex.startsWith('rgb')) {
      return hex.replace(')', `, ${alpha})`).replace('rgb', 'rgba');
    }

    // 简单的 Hex 处理
    if (hex.startsWith('#')) {
      const r = parseInt(hex.slice(1, 3), 16);
      const g = parseInt(hex.slice(3, 5), 16);
      const b = parseInt(hex.slice(5, 7), 16);
      return `rgba(${r}, ${g}, ${b}, ${alpha})`;
    }

    return hex;
  }
}
