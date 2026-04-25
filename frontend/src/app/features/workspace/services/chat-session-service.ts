import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of, tap } from 'rxjs';

import { NativeFetchService } from '../../../core/services/native-fetch-service';
import { PagedResultDto } from '../../../shared/models/paged-result.dto';
import { ChatStreamEvent } from '../../platform/models/chat-stream-event.dto';
import {
  ChatMessageOutputDto,
  ChatModelOptionOutputDto,
  ChatSessionOutputDto,
  CreateChatSessionInputDto,
  GetChatMessagePagedInputDto,
  SendChatMessageInputDto,
  UpdateChatSessionInputDto
} from '../models/chat-session.dto';

@Injectable({
  providedIn: 'root'
})
export class ChatSessionService {
  private static readonly MODEL_OPTIONS_CACHE_TTL = 5 * 60 * 1000;

  private readonly http = inject(HttpClient);
  private readonly nativeFetchService = inject(NativeFetchService);
  private readonly baseUrl = '/api/v1/chat-sessions';
  private readonly modelOptionsCache = new Map<string, { expiresAt: number; value: ChatModelOptionOutputDto[] }>();

  getSessions(): Observable<ChatSessionOutputDto[]> {
    return this.http.get<ChatSessionOutputDto[]>(this.baseUrl);
  }

  getSession(id: string): Observable<ChatSessionOutputDto> {
    return this.http.get<ChatSessionOutputDto>(`${this.baseUrl}/${id}`);
  }

  getMessagePagedList(sessionId: string, input: GetChatMessagePagedInputDto = {}): Observable<PagedResultDto<ChatMessageOutputDto>> {
    let params = new HttpParams().set('limit', input.limit ?? 30);
    if (input.cursorMessageId) {
      params = params.set('cursorMessageId', input.cursorMessageId);
    }

    return this.http.get<PagedResultDto<ChatMessageOutputDto>>(`${this.baseUrl}/${sessionId}/messages`, { params });
  }

  createSession(input: CreateChatSessionInputDto): Observable<ChatSessionOutputDto> {
    return this.http.post<ChatSessionOutputDto>(this.baseUrl, input);
  }

  updateSession(id: string, input: UpdateChatSessionInputDto): Observable<ChatSessionOutputDto> {
    return this.http.put<ChatSessionOutputDto>(`${this.baseUrl}/${id}`, input);
  }

  deleteSession(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getModelOptions(providerGroupId?: string): Observable<ChatModelOptionOutputDto[]> {
    const normalizedProviderGroupId = providerGroupId?.trim() || undefined;
    const cacheKey = normalizedProviderGroupId ?? 'all';
    const cached = this.modelOptionsCache.get(cacheKey);
    if (cached && cached.expiresAt > Date.now()) {
      return of(cached.value);
    }

    const params = normalizedProviderGroupId ? new HttpParams().set('providerGroupId', normalizedProviderGroupId) : undefined;
    return this.http.get<ChatModelOptionOutputDto[]>(`${this.baseUrl}/model-options`, { params }).pipe(
      tap(options => {
        this.modelOptionsCache.set(cacheKey, {
          value: options,
          expiresAt: Date.now() + ChatSessionService.MODEL_OPTIONS_CACHE_TTL
        });
      })
    );
  }

  sendMessage(sessionId: string, input: SendChatMessageInputDto): Observable<ChatStreamEvent> {
    return new Observable(observer => {
      const controller = new AbortController();

      this.nativeFetchService
        .fetch(`${this.baseUrl}/${sessionId}/messages`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json'
          },
          body: JSON.stringify(input),
          signal: controller.signal
        })
        .then(async response => {
          if (!response.ok) {
            observer.error(new Error(await this.readHttpErrorMessage(response)));
            return;
          }

          if (!response.body) {
            observer.error(new Error('聊天响应为空，请稍后重试'));
            return;
          }

          const reader = response.body.getReader();
          const decoder = new TextDecoder();
          let buffer = '';
          let receivedDone = false;

          try {
            while (true) {
              const { done, value } = await reader.read();
              if (done) {
                break;
              }

              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() || '';

              for (const line of lines) {
                const event = this.parseStreamEvent(line);
                if (event === undefined) {
                  continue;
                }

                if (event === null) {
                  receivedDone = true;
                  continue;
                }

                if (event.type === 'Error') {
                  observer.error(new Error(event.content?.trim() || '请求失败，请稍后重试'));
                  return;
                }

                observer.next(event);
              }
            }

            const remaining = this.parseStreamEvent(buffer.trim());
            if (remaining === null) {
              receivedDone = true;
            } else if (remaining) {
              if (remaining.type === 'Error') {
                observer.error(new Error(remaining.content?.trim() || '请求失败，请稍后重试'));
                return;
              }

              observer.next(remaining);
            }

            if (!receivedDone) {
              observer.error(new Error('流式响应提前结束，请稍后重试'));
              return;
            }

            observer.complete();
          } catch (err: unknown) {
            if (err instanceof Error && err.name === 'AbortError') {
              observer.complete();
            } else if (err instanceof Error && err.name === 'TypeError') {
              observer.error(new Error('网络连接异常，请稍后重试'));
            } else {
              observer.error(new Error(err instanceof Error ? err.message || '流式响应中断，请稍后重试' : '流式响应中断，请稍后重试'));
            }
          }
        })
        .catch(err => {
          if (err instanceof Error && err.name === 'TypeError') {
            observer.error(new Error('无法连接到服务器，请检查网络'));
            return;
          }

          observer.error(new Error(err instanceof Error ? err.message || '请求失败，请稍后重试' : '请求失败，请稍后重试'));
        });

      return () => controller.abort();
    });
  }

  private async readHttpErrorMessage(response: Response) {
    const contentType = response.headers.get('Content-Type') || '';
    let message = response.statusText || '请求失败';

    if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
      const json = await response.json();
      if (json && typeof json === 'object') {
        message = (json.message || json.detail || json.error || message) as string;
      }
    } else {
      message = (await response.text()) || message;
    }

    return `API 错误 ${response.status}: ${message}`;
  }

  private parseStreamEvent(line: string): ChatStreamEvent | null | undefined {
    const trimmed = line.trim();
    if (!trimmed || !trimmed.startsWith('data: ')) {
      return undefined;
    }

    const data = trimmed.slice(6).trim();
    if (!data) {
      return undefined;
    }

    if (data === '[DONE]') {
      return null;
    }

    try {
      const event = JSON.parse(data) as Partial<ChatStreamEvent>;
      return {
        type: event.type,
        content: typeof event.content === 'string' ? event.content : undefined,
        isComplete: typeof event.isComplete === 'boolean' ? event.isComplete : undefined,
        inlineData: Array.isArray(event.inlineData) ? event.inlineData : undefined
      };
    } catch {
      return {
        type: 'Error',
        content: data,
        isComplete: true
      };
    }
  }
}
