import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import {
  CreateOpenApplicationInputDto,
  GetOpenApplicationsInputDto,
  OpenApplicationOutputDto,
  ResetOpenApplicationSecretOutputDto,
  UpdateOpenApplicationInputDto
} from '../models/open-application.dto';

@Injectable({
  providedIn: 'root'
})
export class OpenApplicationService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/open-applications';

  getOpenApplications(input?: GetOpenApplicationsInputDto): Observable<PagedResultDto<OpenApplicationOutputDto>> {
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
    if (input?.applicationType) {
      params = params.set('applicationType', input.applicationType);
    }
    if (input?.clientType) {
      params = params.set('clientType', input.clientType);
    }
    if (input?.sorting) {
      params = params.set('sorting', input.sorting);
    }

    return this.http.get<PagedResultDto<OpenApplicationOutputDto>>(this.baseUrl, { params });
  }

  getOpenApplication(id: string): Observable<OpenApplicationOutputDto> {
    return this.http.get<OpenApplicationOutputDto>(`${this.baseUrl}/${id}`);
  }

  createOpenApplication(data: CreateOpenApplicationInputDto): Observable<OpenApplicationOutputDto> {
    return this.http.post<OpenApplicationOutputDto>(this.baseUrl, data);
  }

  updateOpenApplication(id: string, data: UpdateOpenApplicationInputDto): Observable<OpenApplicationOutputDto> {
    return this.http.put<OpenApplicationOutputDto>(`${this.baseUrl}/${id}`, data);
  }

  deleteOpenApplication(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  resetSecret(id: string): Observable<ResetOpenApplicationSecretOutputDto> {
    return this.http.post<ResetOpenApplicationSecretOutputDto>(`${this.baseUrl}/${id}/reset-secret`, {});
  }
}
