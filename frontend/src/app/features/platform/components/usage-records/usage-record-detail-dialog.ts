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
                  @if ((record()?.attemptCount ?? 0) > 1) {
                    <p-tag
                      [value]="'重试×' + (record()!.attemptCount - 1)"
                      severity="warn"
                      styleClass="text-[10px] px-1.5 py-0.5 h-5"
                    ></p-tag>
                  }
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
                <span class="text-xs font-medium text-muted-color uppercase">请求模型</span>
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
            <div class="grid grid-cols-1 md:grid-cols-4 gap-4 pt-4 border-t border-surface-200 dark:border-surface-700">
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
              <p-tab value="1">尝试记录 ({{ detail()?.attempts?.length || 0 }})</p-tab>
            </p-tablist>
            <p-tabpanels>
              <p-tabpanel value="0">
                <div class="flex flex-col gap-4 pt-4">
                  <div class="border border-surface-200 dark:border-surface-700 rounded-lg overflow-hidden">
                    <!-- Header -->
                    <div
                      class="flex items-center gap-3 px-4 py-2.5 bg-surface-50 dark:bg-surface-800 border-b border-surface-200 dark:border-surface-700"
                    >
                      <span class="text-xs font-bold text-muted-color uppercase">下游请求</span>
                      @if (record()?.downStatusCode) {
                        <span class="text-xs font-mono font-bold" [ngClass]="getHttpStatusColorClass(record()?.downStatusCode)">
                          {{ record()?.downStatusCode }}
                        </span>
                      }
                      <span class="text-xs text-muted-color ml-auto">{{ record()?.durationMs || 0 }} ms</span>
                    </div>
                    <!-- Meta -->
                    <div
                      class="px-4 py-2 flex flex-wrap gap-x-6 gap-y-1 text-xs border-b border-surface-200 dark:border-surface-700 bg-surface-0 dark:bg-surface-900"
                    >
                      <span>
                        <span class="text-muted-color">方法：</span>
                        <span class="font-medium font-mono">{{ record()?.downRequestMethod || 'POST' }}</span>
                      </span>
                      @if (detail()?.downRequestUrl) {
                        <span class="truncate max-w-xs">
                          <span class="text-muted-color">URL：</span>
                          <span class="font-mono" [title]="detail()!.downRequestUrl!">{{ detail()?.downRequestUrl }}</span>
                        </span>
                      }
                    </div>
                    <!-- Bodies -->
                    <p-tabs value="req-headers">
                      <p-tablist>
                        <p-tab value="req-headers">Request Headers</p-tab>
                        <p-tab value="req-body">Request Body</p-tab>
                        <p-tab value="resp-body">Response Body</p-tab>
                      </p-tablist>
                      <p-tabpanels>
                        <p-tabpanel value="req-headers">
                          <pre
                            class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-48 custom-scrollbar text-surface-600 dark:text-surface-300"
                            >{{ formatJson(detail()?.downRequestHeaders) || 'N/A' }}</pre
                          >
                        </p-tabpanel>
                        <p-tabpanel value="req-body">
                          <pre
                            class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-[300px] custom-scrollbar text-surface-700 dark:text-surface-200"
                            >{{ formatJson(detail()?.downRequestBody) || 'N/A' }}</pre
                          >
                        </p-tabpanel>
                        <p-tabpanel value="resp-body">
                          <pre
                            class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-[300px] custom-scrollbar text-surface-700 dark:text-surface-200"
                            >{{ formatJson(detail()?.downResponseBody) || 'N/A' }}</pre
                          >
                        </p-tabpanel>
                      </p-tabpanels>
                    </p-tabs>
                  </div>
                </div>
              </p-tabpanel>

              <p-tabpanel value="1">
                <div class="flex flex-col gap-4 pt-4">
                  @if (!detail()?.attempts?.length) {
                    <div class="flex flex-col items-center justify-center h-32 text-muted-color">
                      <i class="pi pi-inbox text-3xl opacity-30 mb-2"></i>
                      <p class="text-sm m-0">暂无尝试记录</p>
                    </div>
                  } @else {
                    @for (attempt of detail()!.attempts; track attempt.attemptNumber) {
                      <div
                        class="border border-surface-200 dark:border-surface-700 rounded-lg overflow-hidden"
                        [class.border-green-300]="attempt.status === 'Success'"
                        [class.dark:border-green-700]="attempt.status === 'Success'"
                        [class.border-red-300]="attempt.status === 'Failed'"
                        [class.dark:border-red-700]="attempt.status === 'Failed'"
                      >
                        <!-- Attempt Header -->
                        <div
                          class="flex items-center gap-3 px-4 py-2.5 bg-surface-50 dark:bg-surface-800 border-b border-surface-200 dark:border-surface-700"
                        >
                          <span class="text-xs font-bold text-muted-color uppercase">尝试 #{{ attempt.attemptNumber }}</span>
                          <p-tag
                            [value]="getStatusLabel(attempt.status)"
                            [severity]="getStatusSeverity(attempt.status)"
                            styleClass="text-[10px] px-1.5 py-0.5 h-5"
                          ></p-tag>
                          @if (attempt.upStatusCode) {
                            <span class="text-xs font-mono font-bold" [ngClass]="getHttpStatusColorClass(attempt.upStatusCode)">{{
                              attempt.upStatusCode
                            }}</span>
                          }
                          <span class="text-xs text-muted-color ml-auto">{{ attempt.durationMs }} ms</span>
                        </div>

                        <!-- Attempt Meta -->
                        <div
                          class="px-4 py-2 flex flex-wrap gap-x-6 gap-y-1 text-xs border-b border-surface-200 dark:border-surface-700 bg-surface-0 dark:bg-surface-900"
                        >
                          <span>
                            <span class="text-muted-color">账户：</span>
                            <span class="font-medium">{{ attempt.accountTokenName }}</span>
                          </span>
                          @if (attempt.upModelId) {
                            <span>
                              <span class="text-muted-color">模型：</span>
                              <span class="font-medium font-mono">{{ attempt.upModelId }}</span>
                            </span>
                          }
                          @if (attempt.upRequestUrl) {
                            <span class="truncate max-w-xs">
                              <span class="text-muted-color">URL：</span>
                              <span class="font-mono" [title]="attempt.upRequestUrl">{{ attempt.upRequestUrl }}</span>
                            </span>
                          }
                          @if (attempt.statusDescription) {
                            <span class="text-red-500 dark:text-red-400 w-full">{{ attempt.statusDescription }}</span>
                          }
                        </div>

                        <!-- Attempt Bodies (collapsible via accordion-like nested tabs) -->
                        <div class="flex flex-col gap-0">
                          <p-tabs value="req-headers">
                            <p-tablist>
                              <p-tab value="req-headers">Request Headers</p-tab>
                              <p-tab value="req-body">Request Body</p-tab>
                              <p-tab value="resp-body">Response Body</p-tab>
                            </p-tablist>
                            <p-tabpanels>
                              <p-tabpanel value="req-headers">
                                <pre
                                  class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-48 custom-scrollbar text-surface-600 dark:text-surface-300"
                                  >{{ formatJson(attempt.upRequestHeaders) || 'N/A' }}</pre
                                >
                              </p-tabpanel>
                              <p-tabpanel value="req-body">
                                <pre
                                  class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-[300px] custom-scrollbar text-surface-700 dark:text-surface-200"
                                  >{{ formatJson(attempt.upRequestBody) || 'N/A' }}</pre
                                >
                              </p-tabpanel>
                              <p-tabpanel value="resp-body">
                                <pre
                                  class="p-3 m-0 font-mono text-xs overflow-auto min-h-[8rem] max-h-[300px] custom-scrollbar text-surface-700 dark:text-surface-200"
                                  >{{ formatJson(attempt.upResponseBody) || 'N/A' }}</pre
                                >
                              </p-tabpanel>
                            </p-tabpanels>
                          </p-tabs>
                        </div>
                      </div>
                    }
                  }
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
      return 'text-green-600 dark:text-green-400';
    } else if (statusCode >= 400 && statusCode < 500) {
      return 'text-orange-600 dark:text-orange-400';
    } else if (statusCode >= 500) {
      return 'text-red-600 dark:text-red-400';
    }

    return 'text-muted-color';
  }

  getModelDisplay(): string {
    const rec = this.record();
    if (!rec) return 'N/A';
    const down = rec.downModelId;
    const up = rec.upModelId;
    if (!down && !up) return 'N/A';
    if (!down) return up || 'N/A';
    if (!up) return down;
    return down === up ? down : `down: ${down} | up: ${up}`;
  }
}
