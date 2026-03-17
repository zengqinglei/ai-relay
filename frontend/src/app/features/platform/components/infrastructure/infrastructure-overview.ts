import { Component, ChangeDetectionStrategy } from '@angular/core';

/**
 * 基础设施概览组件
 */
@Component({
  selector: 'app-infrastructure-overview',
  standalone: true,
  imports: [],
  template: `
    <div class="infrastructure-overview-page p-6">
      <h1 class="text-3xl font-bold text-surface-900 dark:text-surface-0 mb-6"> 基础设施概览 </h1>
      <div class="bg-surface-0 dark:bg-surface-900 rounded-lg p-6 border border-surface-200 dark:border-surface-700">
        <p class="text-surface-600 dark:text-surface-400"> 这里是平台基础设施管理中心，您可以管理服务器、集群、网络和存储资源。 </p>
      </div>
    </div>
  `,
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class InfrastructureOverview {}
