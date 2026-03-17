import { Injectable, inject } from '@angular/core';
import { forkJoin, map, Observable } from 'rxjs';

import { AccountTokenMetricService } from './account-token-metric-service';
import { SubscriptionMetricService } from './subscription-metric-service';
import { UsageRecordMetricService } from './usage-record-metric-service';
import { DashboardViewModel } from '../models/dashboard.dto';

@Injectable({
  providedIn: 'root'
})
export class DashboardService {
  private readonly usageService = inject(UsageRecordMetricService);
  private readonly accountService = inject(AccountTokenMetricService);
  private readonly subscriptionService = inject(SubscriptionMetricService);

  getDashboardData(startTime?: Date, endTime?: Date): Observable<DashboardViewModel> {
    return forkJoin({
      usage: this.usageService.getMetrics(startTime, endTime),
      trend: this.usageService.getTrend(startTime, endTime),
      modelDistribution: this.usageService.getModelDistribution(startTime, endTime),
      apiKeyTrend: this.usageService.getTopApiKeys(startTime, endTime),
      accounts: this.accountService.getMetrics(),
      subscriptions: this.subscriptionService.getMetrics()
    }).pipe(
      map(data => ({
        usage: data.usage,
        trend: data.trend,
        modelDistribution: data.modelDistribution,
        apiKeyTrend: data.apiKeyTrend,
        accounts: data.accounts,
        subscriptions: data.subscriptions
      }))
    );
  }
}
