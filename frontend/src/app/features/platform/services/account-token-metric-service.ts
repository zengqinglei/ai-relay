import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AccountTokenMetricsOutputDto } from '../models/account-token.dto';

@Injectable({
  providedIn: 'root'
})
export class AccountTokenMetricService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/account-tokens/metrics';

  getMetrics(): Observable<AccountTokenMetricsOutputDto> {
    return this.http.get<AccountTokenMetricsOutputDto>(this.baseUrl);
  }
}
