import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { SubscriptionMetricsOutputDto } from '../models/subscription.dto';

@Injectable({
  providedIn: 'root'
})
export class SubscriptionMetricService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/api-keys/metrics';

  getMetrics(): Observable<SubscriptionMetricsOutputDto> {
    return this.http.get<SubscriptionMetricsOutputDto>(this.baseUrl);
  }
}
