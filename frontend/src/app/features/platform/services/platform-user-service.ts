import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { UserOutputDto } from '../../account/models/account.dto';
import { PagedResultDto } from '../../../shared/models/paged-result.dto';

@Injectable({
  providedIn: 'root'
})
export class PlatformUserService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/users';

  getUsers(input?: { offset?: number; limit?: number; keyword?: string; isActive?: boolean }): Observable<PagedResultDto<UserOutputDto>> {
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

    return this.http.get<PagedResultDto<UserOutputDto>>(this.baseUrl, { params });
  }
}
