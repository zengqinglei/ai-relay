import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { NativeFetchService } from '../../../core/services/native-fetch-service';
import { ChatStreamEvent } from '../../platform/models/chat-stream-event.dto';
import {
  ChatModelOptionOutputDto,
  ChatSessionOutputDto,
  CreateChatSessionInputDto,
  SendChatMessageInputDto,
  UpdateChatSessionInputDto
} from '../models/chat-session.dto';

@Injectable({
  providedIn: 'root'
})
export class ChatSessionService {
  private readonly http = inject(HttpClient);
  private readonly nativeFetchService = inject(NativeFetchService);
  private readonly baseUrl = '/api/v1/chat-sessions';

  getSessions(): Observable<ChatSessionOutputDto[]> {
    return this.http.get<ChatSessionOutputDto[]>(this.baseUrl);
  }

  getSession(id: string): Observable<ChatSessionOutputDto> {
    return this.http.get<ChatSessionOutputDto>(`${this.baseUrl}/${id}`);
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

  getModelOptions(): Observable<ChatModelOptionOutputDto[]> {
    return this.http.get<ChatModelOptionOutputDto[]>(`${this.baseUrl}/model-options`);
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
            const contentType = response.headers.get('Content-Type') || '';
            if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
              const json = await response.json();
              observer.error(new Error(json.message || json.detail || response.statusText));
            } else {
              observer.error(new Error((await response.text()) || response.statusText));
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
              if (done) {
                break;
              }

              buffer += decoder.decode(value, { stream: true });
              const lines = buffer.split('\n');
              buffer = lines.pop() || '';

              for (const line of lines) {
                if (!line.trim() || !line.startsWith('data: ')) {
                  continue;
                }

                const dataStr = line.slice(6);
                if (dataStr === '[DONE]') {
                  continue;
                }

                try {
                  observer.next(JSON.parse(dataStr) as ChatStreamEvent);
                } catch {
                  // Ignore malformed chunks from mock/stream.
                }
              }
            }

            const remaining = buffer.trim();
            if (remaining.startsWith('data: ')) {
              const dataStr = remaining.slice(6);
              if (dataStr !== '[DONE]') {
                try {
                  observer.next(JSON.parse(dataStr) as ChatStreamEvent);
                } catch {
                  // Ignore malformed chunks from mock/stream.
                }
              }
            }

            observer.complete();
          } catch (err: unknown) {
            if (err instanceof Error && err.name === 'AbortError') {
              observer.complete();
            } else if (err instanceof Error && err.name === 'TypeError') {
              observer.error(new Error('网络连接异常，请稍后重试'));
            } else {
              observer.error(new Error(err instanceof Error ? err.message : '流式响应中断'));
            }
          }
        })
        .catch(err => observer.error(err));

      return () => controller.abort();
    });
  }
}
