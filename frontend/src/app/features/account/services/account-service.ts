import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map, lastValueFrom } from 'rxjs';

import { AuthService } from '../../../core/services/auth-service';
import { LoginInputDto, LoginOutputDto, ExternalLoginUrlOutputDto, RegisterInputDto } from '../models/account.dto';

@Injectable({ providedIn: 'root' })
export class AccountService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  /**
   * 用户名密码登录
   * 1. 调用登录接口获取 Token
   * 2. 调用 AuthService 保存 Token
   * 3. 调用 AuthService 加载用户信息
   */
  async login(request: LoginInputDto): Promise<void> {
    // 第一步：登录获取 Token
    const loginResponse = await lastValueFrom(this.http.post<LoginOutputDto>('/api/v1/auth/login', request));

    // 第二步：保存 Token
    this.authService.setToken(loginResponse.accessToken);

    // 第三步：获取用户信息
    await lastValueFrom(this.authService.loadUser());
  }

  /**
   * 获取第三方登录 URL
   */
  getExternalLoginUrl(provider: 'github' | 'google'): Observable<ExternalLoginUrlOutputDto> {
    return this.http.get<ExternalLoginUrlOutputDto>(`/api/v1/external-auth/${provider}/login-url`);
  }

  /**
   * 第三方登录回调处理
   */
  async handleExternalLoginCallback(provider: string, code: string, state: string): Promise<void> {
    // 调用后端回调接口
    const loginResponse = await lastValueFrom(
      this.http.post<LoginOutputDto>(`/api/v1/external-auth/${provider}/callback`, {
        code,
        state
      })
    );

    // 保存 Token
    this.authService.setToken(loginResponse.accessToken);

    // 获取用户信息
    await lastValueFrom(this.authService.loadUser());
  }

  /**
   * 用户注册
   */
  register(data: RegisterInputDto): Observable<void> {
    return this.http.post('/api/v1/auth/register', data).pipe(map(() => undefined));
  }
}
