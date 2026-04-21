import { CommonModule, isPlatformBrowser } from '@angular/common';
import { AfterViewChecked, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, PLATFORM_ID, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';

import { LayoutService } from '../../../../layout/services/layout-service';
import { ProviderGroupOutputDto } from '../../../platform/models/provider-group.dto';
import { ProviderGroupService } from '../../../platform/services/provider-group-service';
import {
  ChatModelOptionOutputDto,
  ChatMessageOutputDto,
  ChatSessionOutputDto,
  SendChatMessageInputDto
} from '../../models/chat-session.dto';
import { ChatSessionService } from '../../services/chat-session-service';
import { MessageBubble } from './widgets/message-bubble/message-bubble';

@Component({
  selector: 'app-workspace-chat',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ButtonModule,
    ConfirmPopupModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageBubble,
    SelectModule,
    TextareaModule,
    TooltipModule
  ],
  templateUrl: './workspace-chat.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class WorkspaceChatPage implements AfterViewChecked {
  @ViewChild('messageScrollContainer') private messageScrollContainer?: ElementRef<HTMLDivElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly layoutService = inject(LayoutService);
  private readonly chatSessionService = inject(ChatSessionService);
  private readonly providerGroupService = inject(ProviderGroupService);
  private readonly confirmationService = inject(ConfirmationService);

  readonly sessions = signal<ChatSessionOutputDto[]>([]);
  readonly activeSessionId = signal<string | null>(null);
  readonly routeSessionId = signal<string | null>(null);
  readonly providerGroups = signal<ProviderGroupOutputDto[]>([]);
  readonly modelOptions = signal<ChatModelOptionOutputDto[]>([]);
  readonly loading = signal(false);
  readonly sending = signal(false);
  readonly inputText = signal('');
  readonly sessionListOpen = signal(false);
  readonly sessionSearchQuery = signal('');
  readonly editingTitle = signal(false);
  readonly titleDraft = signal('');

  private shouldAutoScroll = false;

  readonly activeSession = computed(() => this.sessions().find(session => session.id === this.activeSessionId()) ?? null);
  readonly canCreateSession = computed(() => this.providerGroups().length > 0 && this.modelOptions().length > 0);
  readonly filteredSessions = computed(() => {
    const keyword = this.sessionSearchQuery().trim().toLowerCase();
    if (!keyword) {
      return this.sessions();
    }

    return this.sessions().filter(session => {
      const preview = this.getSessionPreview(session).toLowerCase();
      return session.title.toLowerCase().includes(keyword) || preview.includes(keyword);
    });
  });

  constructor() {
    this.layoutService.title.set('聊天');

    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      this.routeSessionId.set(params.get('sessionId'));
      this.syncActiveSession();
      this.sessionListOpen.set(false);
      this.editingTitle.set(false);
      this.requestAutoScroll();
    });

    this.providerGroupService
      .getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(groups => this.providerGroups.set(groups));

    this.chatSessionService
      .getModelOptions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(options => this.modelOptions.set(options));

    this.loadSessions();
  }

  ngAfterViewChecked() {
    if (!this.shouldAutoScroll) {
      return;
    }

    const container = this.messageScrollContainer?.nativeElement;
    if (container) {
      container.scrollTop = container.scrollHeight;
    }

    this.shouldAutoScroll = false;
  }

  loadSessions() {
    this.loading.set(true);
    this.chatSessionService
      .getSessions()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loading.set(false))
      )
      .subscribe(list => {
        this.sessions.set(this.sortSessions(list));
        this.syncActiveSession();
        this.requestAutoScroll();
      });
  }

  onCreateSession() {
    if (!this.canCreateSession()) {
      return;
    }

    const firstGroup = this.providerGroups()[0];
    const firstModel = this.modelOptions()[0];
    if (!firstGroup || !firstModel) {
      return;
    }

    this.chatSessionService
      .createSession({
        title: '新会话',
        providerGroupId: firstGroup.id,
        modelId: firstModel.value
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(session => {
        this.sessions.update(list => this.sortSessions([session, ...list]));
        this.sessionListOpen.set(false);
        this.router.navigate(['/workspace/chat', session.id]);
      });
  }

  onSelectSession(sessionId: string) {
    this.sessionListOpen.set(false);
    this.router.navigate(['/workspace/chat', sessionId]);
  }

  onDeleteSession(event: Event, sessionId: string) {
    event.stopPropagation();
    this.confirmationService.confirm({
      target: event.currentTarget as EventTarget,
      message: '确认删除这个会话？此操作仅删除当前会话记录。',
      acceptLabel: '删除',
      rejectLabel: '取消',
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-text p-button-sm',
      accept: () => this.deleteSession(sessionId)
    });
  }

  onSessionConfigChange(field: 'providerGroupId' | 'modelId', value: string) {
    const session = this.activeSession();
    if (!session || !value) {
      return;
    }

    this.chatSessionService
      .updateSession(session.id, { [field]: value })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(updated => this.upsertSession(updated));
  }

  startRenameSession() {
    const session = this.activeSession();
    if (!session) {
      return;
    }

    this.titleDraft.set(session.title);
    this.editingTitle.set(true);
  }

  cancelRenameSession() {
    this.editingTitle.set(false);
    this.titleDraft.set('');
  }

  saveRenameSession() {
    const session = this.activeSession();
    const title = this.titleDraft().trim();
    if (!session || !title) {
      this.cancelRenameSession();
      return;
    }

    this.chatSessionService
      .updateSession(session.id, { title })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(updated => {
        this.upsertSession(updated);
        this.editingTitle.set(false);
      });
  }

  onRenameKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter') {
      event.preventDefault();
      this.saveRenameSession();
      return;
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      this.cancelRenameSession();
    }
  }

  onSendMessage() {
    const session = this.activeSession();
    const content = this.inputText().trim();
    if (!session || !content || this.sending()) {
      return;
    }

    const now = new Date().toISOString();
    const userMessage: ChatMessageOutputDto = {
      id: `local-user-${Date.now()}`,
      sessionId: session.id,
      role: 'user',
      content,
      creationTime: now
    };
    const assistantMessage: ChatMessageOutputDto = {
      id: `local-ai-${Date.now()}`,
      sessionId: session.id,
      role: 'assistant',
      content: '',
      creationTime: now,
      isStreaming: true
    };

    this.inputText.set('');
    this.sending.set(true);
    this.patchSession(session.id, current => ({
      ...current,
      title: current.title === '新会话' ? this.buildSessionTitle(content) : current.title,
      lastMessageTime: now,
      messages: [...current.messages, userMessage, assistantMessage]
    }));

    const input: SendChatMessageInputDto = { content };
    this.chatSessionService
      .sendMessage(session.id, input)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.sending.set(false);
          this.patchSession(session.id, current => ({
            ...current,
            messages: current.messages.map(message =>
              message.id === assistantMessage.id ? { ...message, isStreaming: false } : message
            )
          }));
        })
      )
      .subscribe({
        next: event => {
          if (event.type === 'Content' && event.content && !event.isComplete) {
            this.patchSession(session.id, current => ({
              ...current,
              lastMessageTime: new Date().toISOString(),
              messages: current.messages.map(message =>
                message.id === assistantMessage.id ? { ...message, content: message.content + event.content } : message
              )
            }));
          }
        },
        error: error => {
          this.patchSession(session.id, current => ({
            ...current,
            messages: current.messages.map(message =>
              message.id === assistantMessage.id ? { ...message, content: `错误：${error.message}`, isStreaming: false } : message
            )
          }));
        }
      });
  }

  onInputKeydown(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.onSendMessage();
    }
  }

  toggleSessionList() {
    this.sessionListOpen.update(value => !value);
  }

  closeSessionList() {
    this.sessionListOpen.set(false);
  }

  isMobileViewport() {
    return isPlatformBrowser(this.platformId) ? window.innerWidth < 1280 : false;
  }

  getSessionPreview(session: ChatSessionOutputDto) {
    const lastMessage = session.messages[session.messages.length - 1];
    if (!lastMessage?.content) {
      return '还没有消息，开始新的对话。';
    }

    return lastMessage.content.replace(/\s+/g, ' ').trim();
  }

  formatRelativeTime(value?: string) {
    if (!value) {
      return '刚刚';
    }

    const diffMs = new Date(value).getTime() - Date.now();
    const diffMinutes = Math.round(diffMs / 60000);
    const rtf = new Intl.RelativeTimeFormat('zh-CN', { numeric: 'auto' });

    if (Math.abs(diffMinutes) < 60) {
      return rtf.format(diffMinutes, 'minute');
    }

    const diffHours = Math.round(diffMinutes / 60);
    if (Math.abs(diffHours) < 24) {
      return rtf.format(diffHours, 'hour');
    }

    const diffDays = Math.round(diffHours / 24);
    return rtf.format(diffDays, 'day');
  }

  trackBySession(_: number, session: ChatSessionOutputDto) {
    return session.id;
  }

  private deleteSession(sessionId: string) {
    this.chatSessionService
      .deleteSession(sessionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const remaining = this.sessions().filter(session => session.id !== sessionId);
        this.sessions.set(this.sortSessions(remaining));

        if (this.activeSessionId() === sessionId) {
          if (remaining.length > 0) {
            this.router.navigate(['/workspace/chat', remaining[0].id]);
          } else {
            this.activeSessionId.set(null);
            this.router.navigate(['/workspace/chat']);
          }
        }
      });
  }

  private syncActiveSession() {
    const sessions = this.sessions();
    const routeSessionId = this.routeSessionId();

    if (routeSessionId) {
      const matched = sessions.find(session => session.id === routeSessionId);
      if (matched) {
        this.activeSessionId.set(matched.id);
        return;
      }
    }

    if (sessions.length > 0) {
      this.activeSessionId.set(sessions[0].id);
      this.router.navigate(['/workspace/chat', sessions[0].id], { replaceUrl: true });
      return;
    }

    this.activeSessionId.set(null);
  }

  private upsertSession(updated: ChatSessionOutputDto) {
    this.sessions.update(list => {
      const next = list.some(session => session.id === updated.id)
        ? list.map(session => (session.id === updated.id ? updated : session))
        : [updated, ...list];
      return this.sortSessions(next);
    });
    this.activeSessionId.set(updated.id);
    this.requestAutoScroll();
  }

  private patchSession(sessionId: string, updater: (session: ChatSessionOutputDto) => ChatSessionOutputDto) {
    this.sessions.update(list => this.sortSessions(list.map(session => (session.id === sessionId ? updater(session) : session))));
    this.requestAutoScroll();
  }

  private sortSessions(list: ChatSessionOutputDto[]) {
    return [...list].sort((a, b) => {
      const timeA = new Date(a.lastMessageTime ?? a.creationTime).getTime();
      const timeB = new Date(b.lastMessageTime ?? b.creationTime).getTime();
      return timeB - timeA;
    });
  }

  private buildSessionTitle(input: string) {
    const normalized = input.replace(/\s+/g, ' ').trim();
    return normalized.length > 20 ? `${normalized.slice(0, 20)}...` : normalized;
  }

  private requestAutoScroll() {
    this.shouldAutoScroll = true;
  }
}
