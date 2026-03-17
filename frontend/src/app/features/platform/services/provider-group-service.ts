import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import {
  CreateProviderGroupInputDto,
  UpdateProviderGroupInputDto,
  GetProviderGroupsInputDto,
  ProviderGroupOutputDto
} from '../models/provider-group.dto';

@Injectable({
  providedIn: 'root'
})
export class ProviderGroupService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/provider-groups';

  getGroups(input?: GetProviderGroupsInputDto): Observable<PagedResultDto<ProviderGroupOutputDto>> {
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
    if (input?.platform) {
      params = params.set('platform', input.platform);
    }
    if (input?.sorting) {
      params = params.set('sorting', input.sorting);
    }

    return this.http.get<PagedResultDto<ProviderGroupOutputDto>>(this.baseUrl, { params });
  }

  getAll(): Observable<ProviderGroupOutputDto[]> {
    return new Observable(observer => {
      this.getGroups({ limit: 1000 }).subscribe({
        next: res => {
          observer.next(res.items);
          observer.complete();
        },
        error: err => observer.error(err)
      });
    });
  }

  getGroup(id: string): Observable<ProviderGroupOutputDto> {
    return this.http.get<ProviderGroupOutputDto>(`${this.baseUrl}/${id}`);
  }

  createGroup(data: CreateProviderGroupInputDto): Observable<ProviderGroupOutputDto> {
    return this.http.post<ProviderGroupOutputDto>(this.baseUrl, data);
  }

  updateGroup(id: string, data: UpdateProviderGroupInputDto): Observable<ProviderGroupOutputDto> {
    // Update logic typically remains separate unless backend also supports aggregate update.
    // Assuming update is still metadata only or user handles accounts separately for now.
    return this.http.put<ProviderGroupOutputDto>(`${this.baseUrl}/${id}`, data);
  }

  deleteGroup(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
