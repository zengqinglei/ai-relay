import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import { UsageRecordDetailOutputDto, UsageRecordOutputDto, UsageRecordPagedInputDto } from '../models/usage.dto';

@Injectable({
  providedIn: 'root'
})
export class UsageRecordService {
  private readonly http = inject(HttpClient);

  getUsageRecords(input: UsageRecordPagedInputDto): Observable<PagedResultDto<UsageRecordOutputDto>> {
    // ✅ 过滤掉 undefined 值，避免传递 "undefined" 字符串
    let params = new HttpParams();
    Object.entries(input).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    });

    return this.http.get<PagedResultDto<UsageRecordOutputDto>>('/api/v1/usage-records', { params });
  }

  getUsageRecordDetail(id: string): Observable<UsageRecordDetailOutputDto> {
    return this.http.get<UsageRecordDetailOutputDto>(`/api/v1/usage-records/${id}/detail`);
  }
}
