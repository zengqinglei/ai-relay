import { CommonModule, isPlatformBrowser } from '@angular/common';
import { AfterViewChecked, ChangeDetectionStrategy, Component, DestroyRef, ElementRef, PLATFORM_ID, ViewChild, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { ConfirmationService } from 'primeng/api';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { ConfirmPopupModule } from 'primeng/confirmpopup';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { MessageModule } from 'primeng/message';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { finalize, map, Subject, switchMap } from 'rxjs';

import { LayoutService } from '../../../../layout/services/layout-service';
import { PlatformIcon } from '../../../../shared/components/platform-icon/platform-icon';
import { MODEL_VENDOR_LABELS } from '../../../../shared/constants/model-vendor.constants';
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
    AutoCompleteModule,
    ButtonModule,
    ConfirmPopupModule,
    IconFieldModule,
    InputIconModule,
    InputTextModule,
    MessageBubble,
    MessageModule,
    PlatformIcon,
    SelectModule,
    TextareaModule,
    TooltipModule
  ],
  templateUrl: './workspace-chat.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [ConfirmationService]
})
export class WorkspaceChatPage implements AfterViewChecked {
  private static readonly MESSAGE_PAGE_SIZE = 30;

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
  readonly activeMessages = signal<ChatMessageOutputDto[]>([]);
  readonly loading = signal(false);
  readonly messagesLoading = signal(false);
  readonly loadingMoreMessages = signal(false);
  readonly hasMoreMessages = signal(false);
  readonly sending = signal(false);
  readonly inputText = signal('');
  readonly sessionListOpen = signal(false);
  readonly sessionSearchQuery = signal('');
  readonly editingTitle = signal(false);
  readonly titleDraft = signal('');
  readonly errorMessage = signal('');
  readonly modelOptionsLoading = signal(false);
  readonly modelSelection = signal<ChatModelOptionOutputDto | string | null>(null);
  readonly filteredModelGroups = signal<Array<{
    label: string;
    vendor?: ChatModelOptionOutputDto['vendor'];
    items: ChatModelOptionOutputDto[];
  }>>([]);
  readonly selectedProviderGroupValue = signal<string | null>(null);

  private shouldAutoScroll = false;
  private loadedMessagesSessionId: string | null = null;
  private cursorMessageId: string | undefined;
  private pendingScrollRestore: { previousHeight: number; previousTop: number } | null = null;
  private modelOptionsGroupId: string | null = null;
  private loadingModelOptionsGroupId: string | null = null;
  private readonly groupChange$ = new Subject<{ sessionId: string; providerGroupId: string | null; currentModelId: string }>();

  readonly activeSession = computed(() => this.sessions().find(session => session.id === this.activeSessionId()) ?? null);
  readonly canCreateSession = computed(() => this.providerGroups().length > 0);
  readonly providerGroupOptions = computed(() => this.providerGroups().map(group => ({
      label: group.name,
      value: group.id,
      icon: group.isPublic ? 'pi pi-globe' : 'pi pi-lock',
      hint: group.isPublic ? '公开分组' : '专属分组'
    })));
  readonly groupedModelOptions = computed(() => {
    const groups = new Map<string, {
      label: string;
      vendor?: ChatModelOptionOutputDto['vendor'];
      items: ChatModelOptionOutputDto[];
    }>();

    for (const option of this.modelOptions()) {
      const vendorKey = option.vendor ?? 'Unknown';
      const vendorLabel = option.vendor ? MODEL_VENDOR_LABELS[option.vendor] : '其他';
      if (!groups.has(vendorKey)) {
        groups.set(vendorKey, { label: vendorLabel, vendor: option.vendor, items: [] });
      }

      groups.get(vendorKey)!.items.push({
        ...option
      });
    }

    return Array.from(groups.values());
  });
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
      this.ensureActiveSessionContext(true);
      this.syncModelDraft();
    });

    this.providerGroupService
      .getVisibleGroups()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(groups => {
        this.providerGroups.set(groups);
        if (!this.activeSession()) {
          this.selectedProviderGroupValue.set(null);
          this.loadModelOptions();
        }
      });

    this.loadSessions();

    this.groupChange$.pipe(
      takeUntilDestroyed(this.destroyRef),
      switchMap(({ sessionId, providerGroupId, currentModelId }) => {
        const targetGroupId = providerGroupId ?? undefined;
        this.modelOptionsLoading.set(true);
        this.filteredModelGroups.set([]);

        return this.chatSessionService.getModelOptions(targetGroupId).pipe(
          map(options => {
            const fallbackModelId = options[0]?.value;
            const nextModelId = options.some(option => option.value === currentModelId)
              ? currentModelId
              : fallbackModelId;

            return {
              groupId: providerGroupId,
              options,
              updateInput: {
                ...(providerGroupId
                  ? { providerGroupId, useAutoProviderGroup: false }
                  : { useAutoProviderGroup: true }),
                ...(nextModelId && nextModelId !== currentModelId ? { modelId: nextModelId } : {})
              }
            };
          }),
          switchMap(plan =>
            this.chatSessionService.updateSession(sessionId, plan.updateInput).pipe(
              map(updated => ({ ...plan, updated }))
            )
          ),
          finalize(() => this.modelOptionsLoading.set(false))
        );
      })
    ).subscribe(({ updated, options, groupId }) => {
      this.modelOptions.set(options);
      this.modelOptionsGroupId = groupId;
      this.filteredModelGroups.set(this.createGroupedModelSuggestions());
      this.upsertSession(updated);
      this.syncModelDraft();
    });
  }

  ngAfterViewChecked() {
    const container = this.messageScrollContainer?.nativeElement;
    if (this.pendingScrollRestore && container) {
      const { previousHeight, previousTop } = this.pendingScrollRestore;
      container.scrollTop = container.scrollHeight - previousHeight + previousTop;
      this.pendingScrollRestore = null;
    }

    if (!this.shouldAutoScroll) {
      return;
    }

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
        this.ensureActiveSessionContext(true);
        this.syncModelDraft();
      });
  }

  onCreateSession() {
    if (!this.canCreateSession()) {
      return;
    }

    const firstModel = this.modelOptions()[0];
    if (firstModel) {
      this.createSession(null, firstModel.value);
      return;
    }

    this.modelOptionsLoading.set(true);
    this.chatSessionService
      .getModelOptions()
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.modelOptionsLoading.set(false))
      )
      .subscribe(options => {
        this.modelOptions.set(options);
        this.modelOptionsGroupId = null;
        this.filteredModelGroups.set(this.createGroupedModelSuggestions());
        const fallbackModel = options[0];
        if (!fallbackModel) {
          return;
        }

        this.createSession(null, fallbackModel.value);
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

  onSessionConfigChange(field: 'providerGroupId' | 'modelId', value: string | null) {
    const session = this.activeSession();
    if (!session) {
      return;
    }

    if (field === 'providerGroupId') {
      this.selectedProviderGroupValue.set(value);
      this.groupChange$.next({ sessionId: session.id, providerGroupId: value, currentModelId: session.modelId });
      return;
    }

    if (!value) {
      return;
    }

    if (field === 'modelId' && !this.selectedProviderGroupValue()) {
      this.chatSessionService
        .updateSession(session.id, { modelId: value, useAutoProviderGroup: true })
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(updated => {
          this.upsertSession(updated);
          this.syncModelDraft();
        });
      return;
    }

    this.chatSessionService
      .updateSession(session.id, { [field]: value })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(updated => {
        this.upsertSession(updated);
        this.syncModelDraft();
      });
  }

  onModelSelectionChange(value: ChatModelOptionOutputDto | string | null | undefined) {
    this.modelSelection.set(value ?? null);
  }

  filterModelSuggestions(event: AutoCompleteCompleteEvent) {
    const query = (event.query ?? '').trim().toLowerCase();
    if (!query) {
      this.filteredModelGroups.set(this.createGroupedModelSuggestions());
      return;
    }

    const filtered = this.createGroupedModelSuggestions()
      .map(group => ({
        ...group,
        items: group.items.filter(option =>
          option.value.toLowerCase().includes(query) || option.label.toLowerCase().includes(query))
      }))
      .filter(group => group.items.length > 0);

    this.filteredModelGroups.set(filtered);
  }

  onModelSelect(value: string | ChatModelOptionOutputDto) {
    const nextValue = typeof value === 'string' ? value : value?.value;
    if (!nextValue) {
      return;
    }

    const selectedOption = this.modelOptions().find(option => option.value === nextValue);
    this.modelSelection.set(selectedOption ?? nextValue);
    this.onSessionConfigChange('modelId', nextValue);
  }

  onModelInputBlur() {
    if (typeof this.modelSelection() === 'string') {
      this.syncModelDraft();
    }
  }

  onMessageScroll() {
    const container = this.messageScrollContainer?.nativeElement;
    if (!container || this.messagesLoading() || this.loadingMoreMessages() || !this.hasMoreMessages()) {
      return;
    }

    if (container.scrollTop <= 80) {
      this.loadMoreMessages();
    }
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

    let assistantContent = '';
    let assistantCompleted = false;

    this.inputText.set('');
    this.sending.set(true);
    this.errorMessage.set('');
    this.activeMessages.update(list => [...list, userMessage, assistantMessage]);
    this.patchSession(session.id, current => ({
      ...current,
      title: current.title === '新会话' ? this.buildSessionTitle(content) : current.title,
      lastMessageTime: now,
      lastMessagePreview: content,
      messageCount: current.messageCount + 1
    }));
    this.requestAutoScroll();

    const input: SendChatMessageInputDto = { content };
    this.chatSessionService
      .sendMessage(session.id, input)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          this.sending.set(false);
          if (assistantCompleted) {
            this.patchSession(session.id, current => ({
              ...current,
              lastMessageTime: new Date().toISOString(),
              lastMessagePreview: assistantContent || current.lastMessagePreview,
              messageCount: current.messageCount + 1
            }));
          }

          this.activeMessages.update(list => list.map(message =>
            message.id === assistantMessage.id ? { ...message, isStreaming: false } : message
          ));
        })
      )
      .subscribe({
        next: event => {
          switch (event.type) {
            case 'Error': {
              this.errorMessage.set(this.resolveErrorMessage(event.content));
              this.activeMessages.update(list => list.filter(item => item.id !== assistantMessage.id));
              this.requestAutoScroll();
              return;
            }
            case 'Content': {
              if (event.inlineData?.length) {
                this.activeMessages.update(list => list.map(message =>
                  message.id === assistantMessage.id
                    ? { ...message, attachments: [...(message.attachments ?? []), ...event.inlineData!] }
                    : message
                ));
                this.requestAutoScroll();
              }

              if (event.content && !event.isComplete) {
                assistantContent += event.content;
                this.activeMessages.update(list => list.map(message =>
                  message.id === assistantMessage.id ? { ...message, content: message.content + event.content } : message
                ));
                this.patchSession(session.id, current => ({
                  ...current,
                  lastMessageTime: new Date().toISOString(),
                  lastMessagePreview: assistantContent
                }));
                this.requestAutoScroll();
                return;
              }

              if (event.isComplete) {
                assistantCompleted = true;
              }
              return;
            }
          }
        },
        error: error => {
          this.errorMessage.set(this.resolveErrorMessage(error));
          this.activeMessages.update(list => list.filter(messageItem => messageItem.id !== assistantMessage.id));
          this.requestAutoScroll();
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
    return session.lastMessagePreview?.trim() || '还没有消息，开始新的对话。';
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

  getProviderGroupName(providerGroupId?: string | null) {
    if (!providerGroupId) {
      return '未选择';
    }

    return this.providerGroups().find(group => group.id === providerGroupId)?.name ?? '未选择';
  }

  getSelectedProviderGroupLabel(session: ChatSessionOutputDto) {
    if (!this.selectedProviderGroupValue()) {
      return '自动';
    }

    return this.getProviderGroupName(session.providerGroupId);
  }

  getSelectedProviderGroupHint() {
    const selectedValue = this.selectedProviderGroupValue();
    if (!selectedValue) {
      return '自动匹配全部可见分组';
    }

    const matched = this.providerGroups().find(group => group.id === selectedValue);
    if (!matched) {
      return '';
    }

    return matched.isPublic ? '公开分组' : '专属分组';
  }

  private syncModelDraft() {
    const modelId = this.activeSession()?.modelId ?? '';
    const selectedOption = this.modelOptions().find(option => option.value === modelId);
    this.modelSelection.set(selectedOption ?? modelId);
    this.filteredModelGroups.set(this.createGroupedModelSuggestions());
  }

  private createSession(providerGroupId: string | null, modelId: string) {
    this.chatSessionService
      .createSession({
        title: '新会话',
        providerGroupId: providerGroupId ?? undefined,
        modelId
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(session => {
        this.sessions.update(list => this.sortSessions([session, ...list]));
        this.sessionListOpen.set(false);
        this.router.navigate(['/workspace/chat', session.id]);
      });
  }

  private loadMoreMessages() {
    const sessionId = this.activeSessionId();
    const cursorMessageId = this.cursorMessageId;
    const container = this.messageScrollContainer?.nativeElement;
    if (!sessionId || !cursorMessageId) {
      return;
    }

    if (container) {
      this.pendingScrollRestore = {
        previousHeight: container.scrollHeight,
        previousTop: container.scrollTop
      };
    }

    this.loadingMoreMessages.set(true);
    this.chatSessionService
      .getMessagePagedList(sessionId, { limit: WorkspaceChatPage.MESSAGE_PAGE_SIZE, cursorMessageId })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.loadingMoreMessages.set(false))
      )
      .subscribe(page => {
        this.cursorMessageId = page.items[0]?.id;
        this.hasMoreMessages.set(page.totalCount > page.items.length);
        this.activeMessages.update(list => [...page.items, ...list]);
      });
  }

  private loadModelOptions(providerGroupId?: string, preferredModelId?: string, sessionId?: string) {
    const groupId = providerGroupId ?? null;

    if (this.loadingModelOptionsGroupId === groupId) {
      return;
    }

    if (this.modelOptionsGroupId === groupId && this.modelOptions().length > 0) {
      if (!sessionId || !preferredModelId) {
        return;
      }

      const exists = this.modelOptions().some(option => option.value === preferredModelId);
      if (exists) {
        return;
      }
    }

    this.loadingModelOptionsGroupId = groupId;
    this.modelOptionsLoading.set(true);
    this.filteredModelGroups.set([]);
    this.chatSessionService
      .getModelOptions(providerGroupId)
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => {
          if (this.loadingModelOptionsGroupId === groupId) {
            this.loadingModelOptionsGroupId = null;
          }
          this.modelOptionsLoading.set(false);
        })
      )
      .subscribe(options => {
        this.modelOptions.set(options);
        this.modelOptionsGroupId = groupId;
        this.filteredModelGroups.set(this.createGroupedModelSuggestions());

        if (!sessionId || !preferredModelId || options.length === 0) {
          this.syncModelDraft();
          return;
        }

        const exists = options.some(option => option.value === preferredModelId);
        if (exists) {
          return;
        }

        const fallbackModel = options[0].value;
        this.chatSessionService
          .updateSession(sessionId, {
            modelId: fallbackModel,
            providerGroupId: groupId ? options[0].providerGroupId : undefined,
            useAutoProviderGroup: !groupId
          })
          .pipe(takeUntilDestroyed(this.destroyRef))
          .subscribe(updated => {
            this.upsertSession(updated);
            this.syncModelDraft();
          });
      });
  }

  private createGroupedModelSuggestions() {
    return this.groupedModelOptions().map(group => ({
      ...group,
      items: [...group.items]
    }));
  }

  private loadSessionMessages(sessionId: string, reset: boolean) {
    if (reset) {
      this.messagesLoading.set(true);
      this.activeMessages.set([]);
      this.cursorMessageId = undefined;
      this.hasMoreMessages.set(false);
    }

    this.chatSessionService
      .getMessagePagedList(sessionId, { limit: WorkspaceChatPage.MESSAGE_PAGE_SIZE })
      .pipe(
        takeUntilDestroyed(this.destroyRef),
        finalize(() => this.messagesLoading.set(false))
      )
      .subscribe(page => {
        this.loadedMessagesSessionId = sessionId;
        this.activeMessages.set(page.items);
        this.cursorMessageId = page.items[0]?.id;
        this.hasMoreMessages.set(page.totalCount > page.items.length);
        this.requestAutoScroll();
      });
  }

  private deleteSession(sessionId: string) {
    this.chatSessionService
      .deleteSession(sessionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const remaining = this.sessions().filter(session => session.id !== sessionId);
        this.sessions.set(this.sortSessions(remaining));

        if (this.activeSessionId() === sessionId) {
          this.loadedMessagesSessionId = null;
          this.activeMessages.set([]);

          if (remaining.length > 0) {
            this.router.navigate(['/workspace/chat', remaining[0].id]);
          } else {
            this.activeSessionId.set(null);
            this.router.navigate(['/workspace/chat']);
          }
        }
      });
  }

  private resolveErrorMessage(error: unknown) {
    if (typeof error === 'string') {
      const message = error.trim();
      return message || '请求失败，请稍后重试';
    }

    if (error instanceof Error) {
      const message = error.message.trim();
      return message || '请求失败，请稍后重试';
    }

    if (error && typeof error === 'object') {
      const message = 'message' in error && typeof error.message === 'string' ? error.message.trim() : '';
      if (message) {
        return message;
      }
    }

    return '请求失败，请稍后重试';
  }

  private syncActiveSession() {
    const sessions = this.sessions();
    const routeSessionId = this.routeSessionId();

    if (routeSessionId) {
      const matched = sessions.find(session => session.id === routeSessionId);
      if (matched) {
        this.activeSessionId.set(matched.id);
        this.selectedProviderGroupValue.set(matched.providerGroupId ?? null);
        return;
      }
    }

    if (sessions.length > 0) {
      this.activeSessionId.set(sessions[0].id);
      this.selectedProviderGroupValue.set(sessions[0].providerGroupId ?? null);
      return;
    }

    this.activeSessionId.set(null);
    this.selectedProviderGroupValue.set(null);
  }

  private ensureActiveSessionContext(resetMessages: boolean) {
    const session = this.activeSession();
    if (!session) {
      this.activeMessages.set([]);
      this.loadedMessagesSessionId = null;
      const defaultProviderGroupId = this.providerGroups()[0]?.id;
      if (defaultProviderGroupId) {
        this.selectedProviderGroupValue.set(null);
        this.loadModelOptions();
      } else {
        this.modelOptions.set([]);
        this.modelOptionsGroupId = null;
        this.filteredModelGroups.set([]);
      }
      return;
    }

    this.loadModelOptions(
      this.selectedProviderGroupValue() ?? undefined,
      session.modelId,
      session.id
    );
    this.syncModelDraft();
    if (resetMessages || this.loadedMessagesSessionId !== session.id) {
      this.loadSessionMessages(session.id, true);
    }
  }

  private upsertSession(updated: ChatSessionOutputDto) {
    this.sessions.update(list => {
      const next = list.some(session => session.id === updated.id)
        ? list.map(session => (session.id === updated.id ? updated : session))
        : [updated, ...list];
      return this.sortSessions(next);
    });
    this.activeSessionId.set(updated.id);
  }

  private patchSession(sessionId: string, updater: (session: ChatSessionOutputDto) => ChatSessionOutputDto) {
    this.sessions.update(list => this.sortSessions(list.map(session => (session.id === sessionId ? updater(session) : session))));
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
    this.scheduleScrollToBottom();
  }

  private scheduleScrollToBottom() {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        const container = this.messageScrollContainer?.nativeElement;
        if (!container) {
          return;
        }

        container.scrollTop = container.scrollHeight;
        this.shouldAutoScroll = false;
      });
    });
  }
}



