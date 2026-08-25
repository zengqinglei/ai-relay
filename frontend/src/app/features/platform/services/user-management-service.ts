import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import {
  CreateUserInputDto,
  GetUsersInputDto,
  ResetUserPasswordInputDto,
  UpdateUserInputDto,
  UserManagementOutputDto
} from '../models/user-management.dto';

@Injectable({ providedIn: 'root' })
export class UserManagementService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/v1/users';

  getUsers(input: GetUsersInputDto): Observable<PagedResultDto<UserManagementOutputDto>> {
    let params = new HttpParams();
    if (input.offset !== undefined) params = params.set('offset', input.offset.toString());
    if (input.limit !== undefined) params = params.set('limit', input.limit.toString());
    if (input.keyword) params = params.set('keyword', input.keyword);
    if (input.isActive !== undefined) params = params.set('isActive', input.isActive.toString());
    if (input.isEmailVerified !== undefined) params = params.set('isEmailVerified', input.isEmailVerified.toString());
    if (input.role) params = params.set('role', input.role);
    if (input.sorting) params = params.set('sorting', input.sorting);
    return this.http.get<PagedResultDto<UserManagementOutputDto>>(this.baseUrl, { params });
  }

  getUser(id: string): Observable<UserManagementOutputDto> {
    return this.http.get<UserManagementOutputDto>(`${this.baseUrl}/${id}`);
  }

  createUser(data: CreateUserInputDto): Observable<UserManagementOutputDto> {
    return this.http.post<UserManagementOutputDto>(this.baseUrl, data);
  }

  updateUser(id: string, data: UpdateUserInputDto): Observable<UserManagementOutputDto> {
    return this.http.put<UserManagementOutputDto>(`${this.baseUrl}/${id}`, data);
  }

  enableUser(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/enable`, {});
  }

  disableUser(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/disable`, {});
  }

  resetPassword(id: string, data: ResetUserPasswordInputDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reset-password`, data);
  }

  deleteUser(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
