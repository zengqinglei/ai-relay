import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { AuthService } from '../../../core/services/auth-service';
import { NativeFetchService } from '../../../core/services/native-fetch-service';
import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import { ProviderPlatform } from '../../../shared/models/provider-platform.enum';
import {
  CreateAccountTokenInputDto,
  GetAccountTokenPagedInputDto,
  AccountTokenOutputDto,
  UpdateAccountTokenInputDto
} from '../models/account-token.dto';
import { ChatMessageInputDto } from '../models/chat-message-input.dto';
import { ChatStreamEvent } from '../models/chat-stream-event.dto';
import { ModelOptionOutputDto } from '../models/model-option.dto';

@Injectable({
  providedIn: 'root'
})
export class AccountTokenService {
  private readonly http = inject(HttpClient);
  private readonly authService = inject(AuthService);
  private readonly nativeFetchService = inject(NativeFetchService);
  private readonly baseUrl = '/api/v1/account-tokens';

  getAccounts(params?: GetAccountTokenPagedInputDto): Observable<PagedResultDto<AccountTokenOutputDto>> {
    let httpParams = new HttpParams();
    if (params?.keyword) httpParams = httpParams.set('keyword', params.keyword);
    if (params?.platform) httpParams = httpParams.set('platform', params.platform);
    if (params?.isActive !== undefined) httpParams = httpParams.set('isActive', params.isActive.toString());
    if (params?.offset !== undefined) httpParams = httpParams.set('offset', params.offset.toString());
    if (params?.limit !== undefined) httpParams = httpParams.set('limit', params.limit.toString());

    return this.http.get<PagedResultDto<AccountTokenOutputDto>>(this.baseUrl, { params: httpParams });
  }

  getAll(): Observable<AccountTokenOutputDto[]> {
    return new Observable(observer => {
      this.getAccounts({ limit: 1000 }).subscribe({
        next: res => {
          observer.next(res.items);
          observer.complete();
        },
        error: err => observer.error(err)
      });
    });
  }

  getAccount(id: string): Observable<AccountTokenOutputDto> {
    return this.http.get<AccountTokenOutputDto>(`${this.baseUrl}/${id}`);
  }

  createAccount(data: CreateAccountTokenInputDto): Observable<AccountTokenOutputDto> {
    return this.http.post<AccountTokenOutputDto>(this.baseUrl, data);
  }

  updateAccount(id: string, data: UpdateAccountTokenInputDto): Observable<AccountTokenOutputDto> {
    return this.http.put<AccountTokenOutputDto>(`${this.baseUrl}/${id}`, data);
  }

  deleteAccount(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  enableAccount(id: string): Observable<void> {
    return this.http.patch<void>(`${this.baseUrl}/${id}/enable`, {});
  }

  disableAccount(id: string) {
    return this.http.patch<void>(`${this.baseUrl}/${id}/disable`, {});
  }

  resetStatus(id: string) {
    return this.http.post<void>(`${this.baseUrl}/${id}/reset-status`, {});
  }

  // OAuth Methods
  getAuthUrl(platform: ProviderPlatform): Observable<{ authUrl: string; sessionId: string }> {
    return this.http.get<{ authUrl: string; sessionId: string }>(`${this.baseUrl}/oauth-url`, {
      params: { platform }
    });
  }

  // Debug Methods

  getAvailableModels(platform: ProviderPlatform, accountId?: string): Observable<ModelOptionOutputDto[]> {
    return this.http.get<ModelOptionOutputDto[]>(`${this.baseUrl}/platform/${platform}/models`, {
      ...(accountId && { params: { accountId } })
    });
  }

  debugModel(id: string, input: ChatMessageInputDto): Observable<ChatStreamEvent> {
    return new Observable(observer => {
      const controller = new AbortController();

      // NativeFetchService 会自动处理 mock 模式和 gateway 前缀
      this.nativeFetchService
        .fetch(`${this.baseUrl}/${id}/model-test`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(input),
          signal: controller.signal
        })
        .then(async response => {
          if (!response.ok) {
            const contentType = response.headers.get('Content-Type') || '';
            if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
              const json = await response.json();
              const message = json.message || json.detail || response.statusText;
              observer.error(new Error(message));
            } else {
              const text = await response.text();
              observer.error(new Error(text || response.statusText));
            }
            return;
          }

          if (!response.body) {
            observer.complete();
            return;
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';

          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) break;

              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() || '';

              for (const line of lines) {
                if (line.trim() === '') continue;
                if (line.startsWith('data: ')) {
                  const dataStr = line.slice(6);
                  if (dataStr === '[DONE]') continue;

                  try {
                    const event = JSON.parse(dataStr) as ChatStreamEvent;
                    observer.next(event);
                  } catch (e) {
                    console.error('Failed to parse SSE data', e);
                  }
                }
              }
            }
            observer.complete();
          } catch (err: any) {
            if (err.name === 'AbortError') {
              observer.complete();
            } else if (err.name === 'TypeError') {
              observer.error(new Error('网络连接异常，请检查网络后重试'));
            } else {
              observer.error(new Error(err.message || '流式响应中断'));
            }
          }
        })
        .catch(err => {
          if (err.name === 'TypeError') {
            observer.error(new Error('无法连接到服务器，请检查网络'));
          } else {
            observer.error(err);
          }
        });

      return () => controller.abort();
    });
  }
}
