import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Permission } from '../../shared/models/permission.model';

@Injectable({ providedIn: 'root' })
export class PermissionService {
  private readonly http = inject(HttpClient);

  /**
   * 获取用户权限
   */
  getPermissions(): Observable<Permission[]> {
    return this.http.get<Permission[]>('/api/v1/auth/permissions');
  }
}
