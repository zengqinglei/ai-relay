import { HttpClient } from '@angular/common/http';
import { Injectable, signal, inject } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { UserOutputDto } from '../../features/account/models/account.dto';
import { User } from '../../shared/models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  private readonly TOKEN_KEY = 'auth_token';
  private _currentUser = signal<User | null>(null);
  public readonly currentUser = this._currentUser.asReadonly();

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }

  setToken(token: string): void {
    localStorage.setItem(this.TOKEN_KEY, token);
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  /**
   * 加载当前用户信息
   */
  loadUser(): Observable<UserOutputDto> {
    return this.http.get<UserOutputDto>('/api/v1/auth/me').pipe(
      tap(user => {
        this.setCurrentUser(user);
      })
    );
  }

  setCurrentUser(user: UserOutputDto): void {
    this._currentUser.set(new User(user));
  }

  hasRole(role: string): boolean {
    return this._currentUser()?.roles.includes(role) ?? false;
  }

  removeToken(): void {
    localStorage.removeItem(this.TOKEN_KEY);
  }

  clearAuthData(): void {
    this.removeToken();
    this._currentUser.set(null);
  }

  /**
   * 用户登出
   */
  logout(): void {
    this.clearAuthData();
  }
}
