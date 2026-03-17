import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { UsageMetricsOutputDto, UsageTrendOutputDto, ApiKeyTrendOutputDto, ModelDistributionOutputDto } from '../models/usage.dto';

@Injectable({
  providedIn: 'root'
})
export class UsageRecordMetricService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/usage/query';

  getMetrics(startTime?: Date, endTime?: Date): Observable<UsageMetricsOutputDto> {
    let params = new HttpParams();
    if (startTime) params = params.set('startTime', startTime.toISOString());
    if (endTime) params = params.set('endTime', endTime.toISOString());
    return this.http.get<UsageMetricsOutputDto>(`${this.baseUrl}/metrics`, { params });
  }

  getTrend(startTime?: Date, endTime?: Date): Observable<UsageTrendOutputDto[]> {
    let params = new HttpParams();
    if (startTime) params = params.set('startTime', startTime.toISOString());
    if (endTime) params = params.set('endTime', endTime.toISOString());
    return this.http.get<UsageTrendOutputDto[]>(`${this.baseUrl}/trend`, { params });
  }

  getTopApiKeys(startTime?: Date, endTime?: Date): Observable<ApiKeyTrendOutputDto[]> {
    let params = new HttpParams();
    if (startTime) params = params.set('startTime', startTime.toISOString());
    if (endTime) params = params.set('endTime', endTime.toISOString());
    return this.http.get<ApiKeyTrendOutputDto[]>(`${this.baseUrl}/top-api-keys`, { params });
  }

  getModelDistribution(startTime?: Date, endTime?: Date): Observable<ModelDistributionOutputDto[]> {
    let params = new HttpParams();
    if (startTime) params = params.set('startTime', startTime.toISOString());
    if (endTime) params = params.set('endTime', endTime.toISOString());
    return this.http.get<ModelDistributionOutputDto[]>(`${this.baseUrl}/model-distribution`, { params });
  }
}
