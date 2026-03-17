import { CommonModule } from '@angular/common';
import { Component, inject, model, signal, effect } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs/operators';

import { DIALOG_CONFIGS } from '../../../../shared/constants/dialog-config.constants';
import { UsageStatus } from '../../../../shared/models/usage-status.enum';
import { formatTokenCount } from '../../../../shared/utils/format.utils';
import { UsageRecordDetailOutputDto, UsageRecordOutputDto } from '../../models/usage.dto';
import { UsageRecordService } from '../../services/usage-record-service';

@Component({
  selector: 'app-usage-record-detail-dialog',
  standalone: true,
  imports: [CommonModule, DialogModule, ButtonModule, TabsModule, TagModule, TooltipModule],
  template: `
    <p-dialog
      header="请求详情"
      [(visible)]="visible"
      [modal]="true"
      [breakpoints]="dialogConfig.breakpoints"
      [style]="dialogConfig.style"
      [contentStyle]="dialogConfig.contentStyle"
      [draggable]="dialogConfig.draggable"
      [resizable]="dialogConfig.resizable"
      (onHide)="onHide()"
    >
      @if (loading()) {
        <div class="flex justify-center items-center h-64">
          <i class="pi pi-spin pi-spinner text-4xl text-primary"></i>
        </div>
      } @else if (detail()) {
        <div class="flex flex-col gap-6">
          <!-- Basic Info Summary - 2行4列布局 -->
          <div
            class="flex flex-col gap-4 p-4 bg-surface-50 dark:bg-surface-800 rounded-lg border border-surface-200 dark:border-surface-700"
          >
            <!-- 第一行：核心信息 -->
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4">
              <!-- 状态 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">状态</span>
                <div class="flex items-center gap-2">
                  <p-tag
                    [value]="getStatusLabel(record()?.status)"
                    [severity]="getStatusSeverity(record()?.status)"
                    styleClass="text-[10px] px-1.5 py-0.5 h-5"
                  ></p-tag>
                  @if (record()?.status === 'Failed' && record()?.statusDescription) {
                    <i
                      class="pi pi-question-circle text-sm text-orange-500 cursor-help"
                      pTooltip="{{ record()?.statusDescription }}"
                      tooltipPosition="right"
                      [escape]="false"
                      tooltipStyleClass="max-w-md"
                    ></i>
                  }
                </div>
              </div>

              <!-- 模型 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">模型</span>
                <span class="text-sm font-medium text-primary truncate" [title]="getModelDisplay()">{{ getModelDisplay() }}</span>
              </div>

              <!-- 请求时间 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">请求时间</span>
                <span class="text-sm font-mono">{{ record()?.creationTime | date: 'yyyy-MM-dd HH:mm:ss' }}</span>
              </div>

              <!-- 耗时 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">耗时</span>
                <span class="text-sm font-bold">{{ record()?.durationMs || 0 }} ms</span>
              </div>
            </div>

            <!-- 第二行：详细信息 -->
            <div class="grid grid-cols-1 md:grid-cols-5 gap-4 pt-4 border-t border-surface-200 dark:border-surface-700">
              <!-- 上游返回 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">上游返回</span>
                <span class="text-sm font-mono font-bold" [ngClass]="getHttpStatusColorClass(record()?.upStatusCode)">
                  {{ record()?.upStatusCode || 'N/A' }}
                </span>
              </div>

              <!-- 返回下游 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">返回下游</span>
                <span class="text-sm font-mono font-bold" [ngClass]="getHttpStatusColorClass(record()?.downStatusCode)">
                  {{ record()?.downStatusCode || 'N/A' }}
                </span>
              </div>

              <!-- 请求类型 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">请求类型</span>
                <p-tag
                  [value]="record()?.isStreaming ? '流式' : '同步'"
                  [severity]="record()?.isStreaming ? 'info' : 'secondary'"
                  styleClass="text-[10px] px-1.5 py-0.5 h-5 w-fit"
                ></p-tag>
              </div>

              <!-- 消耗金额 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">消耗金额</span>
                <span class="text-sm font-bold font-mono text-green-600 dark:text-green-400"
                  >$ {{ record()?.finalCost | number: '1.4-6' }}</span
                >
              </div>

              <!-- Token 统计 -->
              <div class="flex flex-col gap-1.5">
                <span class="text-xs font-medium text-muted-color uppercase">Token 统计</span>
                <div class="flex items-center gap-3">
                  <span class="text-[10px] text-blue-600 dark:text-blue-400" pTooltip="Input Tokens" tooltipPosition="top">
                    <i class="pi pi-arrow-up text-[10px]"></i> {{ formatTokens(record()?.inputTokens) }}
                  </span>
                  <span class="text-[10px] text-purple-600 dark:text-purple-400" pTooltip="Output Tokens" tooltipPosition="top">
                    <i class="pi pi-arrow-down text-[10px]"></i> {{ formatTokens(record()?.outputTokens) }}
                  </span>
                </div>
              </div>
            </div>
          </div>

          <p-tabs value="0">
            <p-tablist>
              <p-tab value="0">下游 (客户端 -> 网关)</p-tab>
              <p-tab value="1">上游 (网关 -> 供应商)</p-tab>
            </p-tablist>
            <p-tabpanels>
              <p-tabpanel value="0">
                <div class="flex flex-col gap-6 pt-4">
                  <div class="flex flex-col gap-2">
                    <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Request URL</label>
                    <div
                      class="p-3 bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md font-mono text-sm break-all flex items-center gap-3"
                    >
                      <span class="font-bold px-2 py-0.5 rounded bg-primary/10 text-primary text-xs">{{
                        record()?.downRequestMethod || 'POST'
                      }}</span>
                      <span class="select-all">{{ detail()?.downRequestUrl }}</span>
                    </div>
                  </div>

                  <div class="grid grid-cols-1 gap-6">
                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Request Headers</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-48 min-h-[3rem] custom-scrollbar text-surface-600 dark:text-surface-300"
                          >{{ formatJson(detail()?.downRequestHeaders) || 'N/A' }}</pre
                        >
                      </div>
                    </div>

                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Request Body</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden relative group"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-[400px] min-h-[3rem] custom-scrollbar text-surface-700 dark:text-surface-200"
                          >{{ formatJson(detail()?.downRequestBody) || 'N/A' }}</pre
                        >
                      </div>
                    </div>

                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Response Body</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-[400px] min-h-[3rem] custom-scrollbar text-surface-700 dark:text-surface-200"
                          >{{ formatJson(detail()?.downResponseBody) || 'N/A' }}</pre
                        >
                      </div>
                    </div>
                  </div>
                </div>
              </p-tabpanel>

              <p-tabpanel value="1">
                <div class="flex flex-col gap-6 pt-4">
                  <div class="flex flex-col gap-2">
                    <label class="font-bold text-sm text-surface-700 dark:text-surface-200">上游 URL & 状态</label>
                    <div class="flex gap-3">
                      <div
                        class="flex-1 p-3 bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md font-mono text-sm break-all select-all"
                      >
                        {{ detail()?.upRequestUrl || 'N/A' }}
                      </div>
                      <div
                        class="w-24 flex items-center justify-center bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md"
                      >
                        <span class="font-mono text-sm font-bold">{{ detail()?.upStatusCode || 'N/A' }}</span>
                      </div>
                    </div>
                  </div>

                  <div class="grid grid-cols-1 gap-6">
                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Request Headers</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-48 min-h-[3rem] custom-scrollbar text-surface-600 dark:text-surface-300"
                          >{{ formatJson(detail()?.upRequestHeaders) || 'N/A' }}</pre
                        >
                      </div>
                    </div>

                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Request Body</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-[400px] min-h-[3rem] custom-scrollbar text-surface-700 dark:text-surface-200"
                          >{{ formatJson(detail()?.upRequestBody) || 'N/A' }}</pre
                        >
                      </div>
                    </div>

                    <div class="flex flex-col gap-2">
                      <label class="font-bold text-sm text-surface-700 dark:text-surface-200">Response Body</label>
                      <div
                        class="bg-surface-50 dark:bg-surface-800 border border-surface-200 dark:border-surface-700 rounded-md overflow-hidden"
                      >
                        <pre
                          class="p-4 m-0 font-mono text-xs overflow-auto max-h-[400px] min-h-[3rem] custom-scrollbar text-surface-700 dark:text-surface-200"
                          >{{ formatJson(detail()?.upResponseBody) || 'N/A' }}</pre
                        >
                      </div>
                    </div>
                  </div>
                </div>
              </p-tabpanel>
            </p-tabpanels>
          </p-tabs>
        </div>
      }

      <ng-template pTemplate="footer">
        <p-button label="关闭" (onClick)="visible.set(false)" styleClass="p-button-text"></p-button>
      </ng-template>
    </p-dialog>
  `
})
export class UsageRecordDetailDialog {
  visible = model<boolean>(false);
  record = model<UsageRecordOutputDto | null>(null);

  detail = signal<UsageRecordDetailOutputDto | null>(null);
  loading = signal<boolean>(false);

  // 使用大型 Dialog 配置
  dialogConfig = DIALOG_CONFIGS.LARGE;

  private readonly usageRecordService = inject(UsageRecordService);

  constructor() {
    // 使用 effect 监听 visible 和 record 的变化，自动加载详情
    effect(() => {
      const isVisible = this.visible();
      const currentRecord = this.record();

      if (isVisible && currentRecord?.id) {
        this.loadDetail(currentRecord.id);
      }
    });
  }

  private loadDetail(id: string) {
    this.loading.set(true);
    this.usageRecordService
      .getUsageRecordDetail(id)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: res => this.detail.set(res),
        error: () => this.detail.set(null)
      });
  }

  onHide() {
    this.detail.set(null);
  }

  formatJson(jsonStr: string | undefined | null): string {
    if (!jsonStr) return '';
    try {
      // Check if it's already an object (though type says string)
      if (typeof jsonStr === 'object') return JSON.stringify(jsonStr, null, 2);
      const obj = JSON.parse(jsonStr);
      return JSON.stringify(obj, null, 2);
    } catch {
      return jsonStr;
    }
  }

  getStatusLabel(status: string | undefined): string {
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

  getStatusSeverity(status: string | undefined): 'success' | 'danger' | 'info' | 'warn' | undefined {
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

  formatTokens(count: number | undefined | null): string {
    return formatTokenCount(count || 0);
  }

  getHttpStatusColorClass(statusCode: number | undefined | null): string {
    if (!statusCode) return 'text-muted-color';

    if (statusCode >= 200 && statusCode < 300) {
      return 'text-green-600 dark:text-green-400'; // 成功 2xx
    } else if (statusCode >= 400 && statusCode < 500) {
      return 'text-orange-600 dark:text-orange-400'; // 客户端错误 4xx
    } else if (statusCode >= 500) {
      return 'text-red-600 dark:text-red-400'; // 服务器错误 5xx
    }

    return 'text-muted-color'; // 其他
  }

  getModelDisplay(): string {
    const rec = this.record();
    if (!rec) return 'N/A';
    const down = rec.downModelId;
    const up = rec.upModelId;
    if (!down && !up) return 'N/A';
    if (!down) return up || 'N/A';
    if (!up) return down;
    return down === up ? down : `down:${down} up:${up}`;
  }
}
