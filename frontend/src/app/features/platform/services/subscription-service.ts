import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import { ApiKeyOutputDto, CreateApiKeyInputDto, GetSubscriptionsInputDto, UpdateApiKeyInputDto } from '../models/subscription.dto';

@Injectable({
  providedIn: 'root'
})
export class SubscriptionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/api-keys';

  getSubscriptions(input?: GetSubscriptionsInputDto): Observable<PagedResultDto<ApiKeyOutputDto>> {
    let params = new HttpParams();

    if (input?.offset !== undefined) {
      params = params.set('offset', input.offset.toString());
    }
    if (input?.limit !== undefined) {
      params = params.set('limit', input.limit.toString());
    }
    if (input?.keyword) {
      params = params.set('keyword', input.keyword);
    }
    if (input?.isActive !== undefined) {
      params = params.set('isActive', input.isActive.toString());
    }
    if (input?.sorting) {
      params = params.set('sorting', input.sorting);
    }

    return this.http.get<PagedResultDto<ApiKeyOutputDto>>(this.baseUrl, { params });
  }

  getSubscription(id: string): Observable<ApiKeyOutputDto> {
    return this.http.get<ApiKeyOutputDto>(`${this.baseUrl}/${id}`);
  }

  createSubscription(data: CreateApiKeyInputDto): Observable<ApiKeyOutputDto> {
    return this.http.post<ApiKeyOutputDto>(this.baseUrl, data);
  }

  updateSubscription(id: string, data: UpdateApiKeyInputDto): Observable<ApiKeyOutputDto> {
    return this.http.put<ApiKeyOutputDto>(`${this.baseUrl}/${id}`, data);
  }

  deleteSubscription(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  toggleStatus(id: string, isActive: boolean, expiresAt?: Date): Observable<void> {
    if (isActive) {
      // Enable 时传递 expiresAt
      const body = expiresAt ? { expiresAt: expiresAt.toISOString() } : {};
      return this.http.patch<void>(`${this.baseUrl}/${id}/enable`, body);
    } else {
      // Disable 时不传递任何参数
      return this.http.patch<void>(`${this.baseUrl}/${id}/disable`, {});
    }
  }
}
