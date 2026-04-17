# 工作区聊天功能设计方案

> 版本：v1.0 | 日期：2026-04-17 | 状态：待实现

---

## 一、功能目标

在 `/workspace` 路由下实现类 Gemini/ChatGPT 风格的 AI 聊天界面，支持：

- 多会话管理（创建、切换、删除）
- 每会话独立配置（分组、账户、模型）
- SSE 流式响应、Markdown 渲染
- 图片/视频附件的展示与下载
- 后端持久化（会话 + 消息）

分两阶段交付：

- **Phase 1**：纯前端 + Mock 数据，完整交互可用
- **Phase 2**：接入后端真实 API（持久化、流式推送）

---

## 二、布局设计

### 2.1 总体结构

沿用现有 `DefaultLayout`（Sidebar + Header + router-outlet），**不引入额外的内部 Splitter**。

工作区下 Sidebar 默认展开（`sidebarCollapsed = false`），菜单项如下：

```
├── 💬 聊天               ← 带子菜单（会话列表）
│   ├── [+ 新建会话]       ← 始终置顶的操作按钮
│   ├── 会话 A            ← 动态列表，点击切换
│   ├── 会话 B
│   └── ...
├── 🔑 ApiKey 使用情况
└── 📊 仪表盘
```

聊天主区域（`router-outlet` 内容）布局：

```
┌──────────────────────────────────────────┐
│  [会话标题]  [分组▼] [账户▼] [模型▼]  [...] │  ← 配置头（chat-toolbar）
├──────────────────────────────────────────┤
│                                          │
│           消息列表（message-list）          │
│                                          │
├──────────────────────────────────────────┤
│  [📎] [ 输入框（autoResize）         ] [发送] │  ← 输入区
└──────────────────────────────────────────┘
```

### 2.2 Sidebar 子菜单扩展规则

`DefaultSidebar` 当前只支持一级扁平菜单。需扩展 `MenuItem` 接口以支持 `children`，并在模板中条件渲染子列表。

子菜单仅在 Sidebar 展开时可见；折叠时，鼠标悬停父项显示 tooltip，子菜单隐藏。

---

## 三、文件变更树

### 3.1 前端（Phase 1）

```
frontend/
├── _mock/
│   ├── data/
│   │   └── workspace-chat.ts                    ← 新增：会话 + 消息 Mock 数据
│   ├── api/
│   │   └── workspace-chat.ts                    ← 新增：HTTP + SSE Mock 处理器
│   └── index.ts                                 ← 修改：export * from './api/workspace-chat'
│
└── src/app/
    ├── features/workspace/
    │   ├── models/
    │   │   └── chat-session.dto.ts               ← 新增：会话 + 消息 DTO
    │   ├── services/
    │   │   └── chat-session-service.ts           ← 新增：会话 CRUD + SSE 流
    │   ├── components/chat/
    │   │   ├── workspace-chat.ts                 ← 新增：Page 组件
    │   │   ├── workspace-chat.html
    │   │   └── widgets/
    │   │       ├── chat-toolbar/
    │   │       │   ├── chat-toolbar.ts           ← 新增：分组/账户/模型配置头
    │   │       │   └── chat-toolbar.html
    │   │       └── message-bubble/
    │   │           ├── message-bubble.ts         ← 新增：单条消息气泡（Markdown + 图片）
    │   │           └── message-bubble.html
    │   └── workspace.routes.ts                   ← 修改：指向 WorkspaceChatPage
    │
    └── layout/components/default-sidebar/
        ├── default-sidebar.ts                    ← 修改：扩展 MenuItem 支持 children
        └── default-sidebar.html                  ← 修改：渲染子菜单
```

### 3.2 后端（Phase 2）

```
backend/src/
├── AiRelay.Domain/
│   └── ChatSessions/
│       ├── Entities/
│       │   ├── ChatSession.cs                    ← 新增：会话聚合根
│       │   └── ChatMessage.cs                    ← 新增：消息实体
│       └── Repositories/
│           └── IChatSessionRepository.cs         ← 新增：仓储接口
│
├── AiRelay.Application/
│   └── ChatSessions/
│       ├── Dtos/
│       │   ├── ChatSessionOutputDto.cs
│       │   ├── ChatMessageOutputDto.cs
│       │   ├── CreateChatSessionInputDto.cs
│       │   └── SendChatMessageInputDto.cs
│       ├── Mappings/
│       │   └── ChatSessionProfile.cs             ← Mapster 映射
│       └── AppServices/
│           ├── IChatSessionAppService.cs
│           └── ChatSessionAppService.cs
│
├── AiRelay.Infrastructure/
│   └── ChatSessions/
│       └── ChatSessionRepository.cs              ← EF Core 仓储实现
│
└── AiRelay.Api/
    └── Controllers/
        └── ChatSessionController.cs              ← REST + SSE 端点
```

---

## 四、类型复用原则

### 4.0 与 `ChatStreamEvent` / `InlineDataPart` 的关系

`ChatStreamEvent` 是**流协议结构**（SSE 传输中的单个 chunk），`ChatMessageOutputDto` 是**持久化消息结构**（完整内容，用于历史渲染），二者职责不同、不互相替代。

| | `ChatStreamEvent` | `ChatMessageOutputDto` |
|---|---|---|
| 生命周期 | 流式传输过程中逐块产生，用完即丢 | 完整消息，持久化/缓存于会话列表 |
| content | 每块只有部分字符串 | 流结束后拼接完整的 Markdown 文本 |
| 使用场景 | `observer.next()` 时实时追加 | 会话加载、消息列表渲染 |

**可以复用的部分：**

- `InlineDataPart`（来自 `chat-stream-event.dto.ts`）描述图片/媒体数据，与消息附件结构完全一致，**直接复用，不重新定义**。
- `ContentBlock`（目前在 `ModelTestDialog` 内部声明为局部类型）描述渲染时的内容块（文本/图片），两处都需要 → **提取到 `shared/models/content-block.ts`**。

**文件变更树补充：**

```
frontend/src/app/shared/models/
└── content-block.ts    ← 新增：从 ModelTestDialog 提取，聊天气泡共用
```

```typescript
// shared/models/content-block.ts
export type ContentBlock =
  | { type: 'text'; text: string }
  | { type: 'image'; mimeType: string; data?: string; url?: string };
```

`ModelTestDialog` 中 `export type ContentBlock = ...` 那行改为 `import { ContentBlock } from '../../../../../shared/models/content-block'`。

---

## 五、核心 DTO

### 5.1 前端 `chat-session.dto.ts`

```typescript
// frontend/src/app/features/workspace/models/chat-session.dto.ts
import { InlineDataPart } from '../../../platform/models/chat-stream-event.dto';

export type ChatMessageRole = 'user' | 'assistant' | 'system';

export interface ChatMessageOutputDto {
  id: string;
  sessionId: string;
  role: ChatMessageRole;
  content: string;               // Markdown 文本（流结束后完整内容）
  attachments?: InlineDataPart[]; // 复用 InlineDataPart，不重复定义
  creationTime: string;
  isStreaming?: boolean;          // 前端临时字段，不持久化
}

export interface ChatSessionOutputDto {
  id: string;
  title: string;
  providerGroupId: string;
  accountId?: string;            // null = 自动调度
  modelId: string;
  messages: ChatMessageOutputDto[];
  creationTime: string;
  lastMessageTime?: string;
}

export interface CreateChatSessionInputDto {
  title?: string;
  providerGroupId: string;
  accountId?: string;
  modelId: string;
}

export interface UpdateChatSessionInputDto {
  title?: string;
  providerGroupId?: string;
  accountId?: string;
  modelId?: string;
}

export interface SendChatMessageInputDto {
  content: string;
  attachments?: InlineDataPart[]; // 复用 InlineDataPart
}

export interface GetChatSessionsInputDto {
  keyword?: string;
  offset?: number;
  limit?: number;
}
```

---

## 六、前端服务

### 5.1 `chat-session-service.ts`

```typescript
// frontend/src/app/features/workspace/services/chat-session-service.ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { NativeFetchService } from '../../../core/services/native-fetch-service';
import { ChatStreamEvent } from '../../platform/models/chat-stream-event.dto';
import {
  ChatSessionOutputDto,
  CreateChatSessionInputDto,
  GetChatSessionsInputDto,
  SendChatMessageInputDto,
  UpdateChatSessionInputDto
} from '../models/chat-session.dto';

@Injectable({ providedIn: 'root' })
export class ChatSessionService {
  private readonly http = inject(HttpClient);
  private readonly nativeFetchService = inject(NativeFetchService);
  private readonly baseUrl = '/api/v1/chat-sessions';

  getSessions(params?: GetChatSessionsInputDto): Observable<ChatSessionOutputDto[]> {
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

  sendMessage(sessionId: string, input: SendChatMessageInputDto): Observable<ChatStreamEvent> {
    return new Observable(observer => {
      const controller = new AbortController();

      this.nativeFetchService
        .fetch(`${this.baseUrl}/${sessionId}/messages`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
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

          if (!response.body) { observer.complete(); return; }

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
                if (!line.trim() || !line.startsWith('data: ')) continue;
                const dataStr = line.slice(6);
                if (dataStr === '[DONE]') continue;
                try { observer.next(JSON.parse(dataStr) as ChatStreamEvent); } catch { /* ignore */ }
              }
            }
            observer.complete();
          } catch (err: unknown) {
            if (err instanceof Error && err.name === 'AbortError') {
              observer.complete();
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
```

---

## 七、Mock 数据 & API

### 6.1 `_mock/data/workspace-chat.ts`

```typescript
// frontend/_mock/data/workspace-chat.ts
import { ChatMessageOutputDto, ChatSessionOutputDto } from '../../src/app/features/workspace/models/chat-session.dto';

export const CHAT_SESSIONS: ChatSessionOutputDto[] = [
  {
    id: 'session-1',
    title: 'Gemini 2.5 Pro 探索',
    providerGroupId: 'group-gemini-shared',
    accountId: undefined,
    modelId: 'gemini-2.5-pro',
    creationTime: new Date(Date.now() - 86400000).toISOString(),
    lastMessageTime: new Date(Date.now() - 3600000).toISOString(),
    messages: [
      {
        id: 'msg-1-1',
        sessionId: 'session-1',
        role: 'user',
        content: '请介绍一下 Gemini 2.5 Pro 的主要特性。',
        creationTime: new Date(Date.now() - 3700000).toISOString()
      },
      {
        id: 'msg-1-2',
        sessionId: 'session-1',
        role: 'assistant',
        content: `## Gemini 2.5 Pro 主要特性\n\nGemini 2.5 Pro 是 Google 最新一代多模态大语言模型，具备以下核心能力：\n\n- **超长上下文**：支持 100 万 token 上下文窗口\n- **多模态理解**：原生支持文本、图片、视频、音频输入\n- **代码能力**：在主流编程基准上达到 SOTA 水平\n- **推理增强**：内置 chain-of-thought 推理链路`,
        creationTime: new Date(Date.now() - 3600000).toISOString()
      }
    ]
  },
  {
    id: 'session-2',
    title: 'Claude 代码审查',
    providerGroupId: 'group-default',
    accountId: undefined,
    modelId: 'claude-sonnet-4-6',
    creationTime: new Date(Date.now() - 7200000).toISOString(),
    lastMessageTime: new Date(Date.now() - 7200000).toISOString(),
    messages: [
      {
        id: 'msg-2-1',
        sessionId: 'session-2',
        role: 'user',
        content: '帮我 review 一下这段 TypeScript 代码的类型安全问题。',
        creationTime: new Date(Date.now() - 7200000).toISOString()
      }
    ]
  }
];

export const MOCK_WORKSPACE_STREAM_CHUNKS = [
  { type: 'Content', content: '好的，我来帮您分析这个问题。\n\n' },
  { type: 'Content', content: '根据您的描述，' },
  { type: 'Content', content: '这里有几个需要注意的点：\n\n' },
  { type: 'Content', content: '1. **类型推断**：TypeScript 编译器在这里无法正确推断类型\n' },
  { type: 'Content', content: '2. **空值处理**：建议使用可选链操作符 `?.` 避免运行时错误\n' },
  { type: 'Content', content: '3. **泛型约束**：可以通过添加 `extends` 约束来收窄类型范围\n\n' },
  { type: 'Content', content: '如果需要进一步分析，请提供完整代码片段。' },
  { type: 'Content', isComplete: true }
];
```

### 6.2 `_mock/api/workspace-chat.ts`

```typescript
// frontend/_mock/api/workspace-chat.ts
import { MockRequest, MockResponse } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { CHAT_SESSIONS, MOCK_WORKSPACE_STREAM_CHUNKS } from '../data/workspace-chat';
import { ChatSessionOutputDto, CreateChatSessionInputDto, UpdateChatSessionInputDto } from '../../src/app/features/workspace/models/chat-session.dto';

// 内存状态（Mock 运行期间持久）
let sessions = [...CHAT_SESSIONS];

// ---------- REST ----------

export function getWorkspaceChatSessions(_req: MockRequest): MockResponse {
  const sorted = [...sessions].sort(
    (a, b) => new Date(b.lastMessageTime ?? b.creationTime).getTime() - new Date(a.lastMessageTime ?? a.creationTime).getTime()
  );
  return { status: 200, body: sorted };
}

export function getWorkspaceChatSession(req: MockRequest): MockResponse {
  const session = sessions.find(s => s.id === req.params['id']);
  if (!session) return { status: 404, body: { message: '会话不存在' } };
  return { status: 200, body: session };
}

export function createWorkspaceChatSession(req: MockRequest): MockResponse {
  const input = req.body as CreateChatSessionInputDto;
  const now = new Date().toISOString();
  const newSession: ChatSessionOutputDto = {
    id: `session-${Date.now()}`,
    title: input.title || '新会话',
    providerGroupId: input.providerGroupId,
    accountId: input.accountId,
    modelId: input.modelId,
    messages: [],
    creationTime: now,
    lastMessageTime: now
  };
  sessions = [newSession, ...sessions];
  return { status: 200, body: newSession };
}

export function updateWorkspaceChatSession(req: MockRequest): MockResponse {
  const idx = sessions.findIndex(s => s.id === req.params['id']);
  if (idx === -1) return { status: 404, body: { message: '会话不存在' } };
  const input = req.body as UpdateChatSessionInputDto;
  sessions[idx] = { ...sessions[idx], ...input };
  return { status: 200, body: sessions[idx] };
}

export function deleteWorkspaceChatSession(req: MockRequest): MockResponse {
  const idx = sessions.findIndex(s => s.id === req.params['id']);
  if (idx === -1) return { status: 404, body: { message: '会话不存在' } };
  sessions.splice(idx, 1);
  return { status: 200, body: null };
}

// ---------- SSE ----------

SSE_MOCK_REGISTRY.register(/\/api\/v1\/chat-sessions\/([^/]+)\/messages$/, async function* (req) {
  const userContent = (req.body as { content: string })?.content || '';
  const sessionId = req.url.match(/\/chat-sessions\/([^/]+)\/messages/)?.[1];

  // 把 user 消息写入 mock 内存
  if (sessionId) {
    const session = sessions.find(s => s.id === sessionId);
    if (session) {
      const now = new Date().toISOString();
      session.messages.push({
        id: `msg-${Date.now()}-user`,
        sessionId,
        role: 'user',
        content: userContent,
        creationTime: now
      });
      session.lastMessageTime = now;
    }
  }

  // 模拟流式输出
  for (const chunk of MOCK_WORKSPACE_STREAM_CHUNKS) {
    await new Promise(r => setTimeout(r, 80));
    yield chunk;
  }

  // 把 assistant 消息写入 mock 内存
  if (sessionId) {
    const session = sessions.find(s => s.id === sessionId);
    if (session) {
      const fullContent = MOCK_WORKSPACE_STREAM_CHUNKS.filter(c => c.content && !c.isComplete).map(c => c.content).join('');
      session.messages.push({
        id: `msg-${Date.now()}-assistant`,
        sessionId,
        role: 'assistant',
        content: fullContent,
        creationTime: new Date().toISOString()
      });
    }
  }
});

// ---------- Mock API 路由表（由 MockInterceptor 消费）----------

export const WORKSPACE_CHAT_MOCK_APIS = [
  { method: 'GET',    url: /\/api\/v1\/chat-sessions$/,              handler: getWorkspaceChatSessions },
  { method: 'GET',    url: /\/api\/v1\/chat-sessions\/([^/]+)$/,     handler: getWorkspaceChatSession },
  { method: 'POST',   url: /\/api\/v1\/chat-sessions$/,              handler: createWorkspaceChatSession },
  { method: 'PUT',    url: /\/api\/v1\/chat-sessions\/([^/]+)$/,     handler: updateWorkspaceChatSession },
  { method: 'DELETE', url: /\/api\/v1\/chat-sessions\/([^/]+)$/,     handler: deleteWorkspaceChatSession }
];
```

> **注意**：`_mock/index.ts` 需补充：
> ```typescript
> export * from './api/workspace-chat';
> ```
> 同时 `MockInterceptor` 的路由表需 import `WORKSPACE_CHAT_MOCK_APIS` 并合并进 `MOCK_APIS` 数组。

---

## 八、Sidebar 扩展

### 7.1 `default-sidebar.ts` 修改

```typescript
// 扩展 MenuItem 接口
interface MenuItem {
  label: string;
  icon: string;
  route?: string;                // 可选：无 route 的父项只展开子菜单
  children?: SessionMenuItem[];
  actionLabel?: string;          // 子菜单顶部操作按钮文字
  onAction?: () => void;
}

interface SessionMenuItem {
  id: string;
  label: string;
  route: string;
}

// WorkspaceMenuItems 变为动态 computed
// 在 WorkspaceChatPage 通过 LayoutService 注入会话列表 signal
```

实际实现中，`DefaultSidebar` 本身无法直接访问 `ChatSessionService`（职责混淆）。推荐做法：在 `LayoutService` 中暴露一个 `workspaceSessions = signal<{id:string; title:string}[]>([])` ，由 `WorkspaceChatPage` 在加载会话后 `set`，`DefaultSidebar` 订阅该 signal 动态生成子菜单。

```typescript
// layout-service.ts 新增
workspaceSessions = signal<{ id: string; title: string }[]>([]);
```

```typescript
// default-sidebar.ts 新增 computed
private readonly layoutService = inject(LayoutService);

readonly workspaceMenuItems = computed(() => [
  {
    label: '聊天',
    icon: 'pi-comments',
    route: '/workspace/chat',
    children: this.layoutService.workspaceSessions().map(s => ({
      id: s.id,
      label: s.title,
      route: `/workspace/chat/${s.id}`
    })),
    actionLabel: '新建会话',
    onAction: () => this.router.navigate(['/workspace/chat/new'])
  },
  { label: 'ApiKey 使用情况', icon: 'pi-key', route: '/workspace/apikeys' },
  { label: '仪表盘', icon: 'pi-gauge', route: '/workspace/dashboard' }
]);
```

### 7.2 `default-sidebar.html` 子菜单渲染片段

```html
@for (item of menuItems(); track item.route ?? item.label) {
  <li>
    @if (item.children) {
      <!-- 父项（可展开） -->
      <a
        [routerLink]="item.route"
        routerLinkActive="!text-primary-600 dark:!text-primary-400 font-bold"
        class="flex items-center cursor-pointer px-3 py-3 rounded-md text-color hover:bg-surface-100 dark:hover:bg-surface-800 transition-all duration-300"
        [class.gap-3]="!layoutService.sidebarCollapsed() || isMobileMenuOpen()"
        [class.gap-0]="layoutService.sidebarCollapsed() && !isMobileMenuOpen()"
      >
        <div class="w-8 h-8 flex items-center justify-center shrink-0">
          <i class="pi {{ item.icon }} text-xl"></i>
        </div>
        <span class="font-medium flex-1 whitespace-nowrap overflow-hidden transition-all duration-300"
          [class.max-w-0]="layoutService.sidebarCollapsed() && !isMobileMenuOpen()"
          [class.opacity-0]="layoutService.sidebarCollapsed() && !isMobileMenuOpen()"
          [class.max-w-60]="!layoutService.sidebarCollapsed() || isMobileMenuOpen()"
          [class.opacity-100]="!layoutService.sidebarCollapsed() || isMobileMenuOpen()">
          {{ item.label }}
        </span>
        @if (!layoutService.sidebarCollapsed() || isMobileMenuOpen()) {
          <!-- 新建按钮 -->
          <button
            *ngIf="item.onAction"
            class="w-6 h-6 flex items-center justify-center rounded hover:bg-surface-200 dark:hover:bg-surface-700 shrink-0"
            [pTooltip]="item.actionLabel"
            tooltipPosition="right"
            (click)="$event.preventDefault(); item.onAction && item.onAction()"
          >
            <i class="pi pi-plus text-xs"></i>
          </button>
        }
      </a>

      <!-- 子菜单列表（展开时显示） -->
      @if (!layoutService.sidebarCollapsed() || isMobileMenuOpen()) {
        <ul class="pl-10 mt-1 space-y-0.5 list-none p-0 m-0">
          @for (child of item.children; track child.id) {
            <li>
              <a
                [routerLink]="child.route"
                routerLinkActive="!text-primary-600 dark:!text-primary-400 font-semibold bg-surface-100 dark:bg-surface-800"
                class="block px-2 py-1.5 rounded text-sm text-color hover:bg-surface-100 dark:hover:bg-surface-800 truncate cursor-pointer"
              >
                {{ child.label }}
              </a>
            </li>
          }
        </ul>
      }
    } @else {
      <!-- 普通菜单项（原有逻辑） -->
      <a [routerLink]="item.route" ...>...</a>
    }
  </li>
}
```

---

## 九、页面组件

### 8.1 路由配置

```typescript
// workspace.routes.ts
export const WORKSPACE_ROUTES: Routes = [
  {
    path: '',
    redirectTo: 'chat',
    pathMatch: 'full'
  },
  {
    path: 'chat',
    loadComponent: () => import('./components/chat/workspace-chat').then(m => m.WorkspaceChatPage)
  },
  {
    path: 'chat/:sessionId',
    loadComponent: () => import('./components/chat/workspace-chat').then(m => m.WorkspaceChatPage)
  }
];
```

### 8.2 `workspace-chat.ts`（Page 组件）

```typescript
// frontend/src/app/features/workspace/components/chat/workspace-chat.ts
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { finalize } from 'rxjs';

import { LayoutService } from '../../../../layout/services/layout-service';
import { ProviderGroupOutputDto } from '../../platform/models/provider-group.dto';
import { ModelOptionOutputDto } from '../../platform/models/model-option.dto';
import { ProviderGroupService } from '../../platform/services/provider-group-service';
import { AccountTokenService } from '../../platform/services/account-token-service';
import { ChatMessageOutputDto, ChatSessionOutputDto, SendChatMessageInputDto } from '../models/chat-session.dto';
import { ChatSessionService } from '../services/chat-session-service';
import { ChatToolbar } from './widgets/chat-toolbar/chat-toolbar';
import { MessageBubble } from './widgets/message-bubble/message-bubble';

@Component({
  selector: 'app-workspace-chat',
  standalone: true,
  imports: [CommonModule, ChatToolbar, MessageBubble],
  templateUrl: './workspace-chat.html'
})
export class WorkspaceChatPage implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly layoutService = inject(LayoutService);
  private readonly chatSessionService = inject(ChatSessionService);
  private readonly providerGroupService = inject(ProviderGroupService);
  private readonly accountTokenService = inject(AccountTokenService);

  sessions = signal<ChatSessionOutputDto[]>([]);
  activeSession = signal<ChatSessionOutputDto | null>(null);
  providerGroups = signal<ProviderGroupOutputDto[]>([]);
  availableModels = signal<ModelOptionOutputDto[]>([]);

  loading = signal(false);
  sending = signal(false);
  inputText = signal('');

  ngOnInit() {
    this.layoutService.title.set('工作区');
    this.layoutService.sidebarCollapsed.set(false);

    this.providerGroupService.getAll()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(groups => this.providerGroups.set(groups));

    this.loadSessions();

    this.route.paramMap
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(params => {
        const sessionId = params.get('sessionId');
        if (sessionId) {
          this.activateSession(sessionId);
        }
      });
  }

  loadSessions() {
    this.chatSessionService.getSessions()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(sessions => {
        this.sessions.set(sessions);
        this.layoutService.workspaceSessions.set(sessions.map(s => ({ id: s.id, title: s.title })));

        // 默认打开第一个会话
        if (!this.activeSession() && sessions.length > 0) {
          this.router.navigate(['/workspace/chat', sessions[0].id], { replaceUrl: true });
        }
      });
  }

  activateSession(sessionId: string) {
    const existing = this.sessions().find(s => s.id === sessionId);
    if (existing) {
      this.activeSession.set(existing);
    } else {
      this.chatSessionService.getSession(sessionId)
        .pipe(takeUntilDestroyed(this.destroyRef))
        .subscribe(session => this.activeSession.set(session));
    }
  }

  onCreateSession(input: { providerGroupId: string; modelId: string; accountId?: string }) {
    this.chatSessionService.createSession({ ...input, title: '新会话' })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(session => {
        this.sessions.update(list => [session, ...list]);
        this.layoutService.workspaceSessions.update(list => [{ id: session.id, title: session.title }, ...list]);
        this.router.navigate(['/workspace/chat', session.id]);
      });
  }

  onSendMessage(text: string) {
    const session = this.activeSession();
    if (!session || !text.trim() || this.sending()) return;

    const input: SendChatMessageInputDto = { content: text.trim() };

    // 乐观追加 user 消息
    const userMsg: ChatMessageOutputDto = {
      id: `tmp-${Date.now()}`,
      sessionId: session.id,
      role: 'user',
      content: text.trim(),
      creationTime: new Date().toISOString()
    };
    const assistantMsg: ChatMessageOutputDto = {
      id: `tmp-assistant-${Date.now()}`,
      sessionId: session.id,
      role: 'assistant',
      content: '',
      creationTime: new Date().toISOString(),
      isStreaming: true
    };

    this.activeSession.update(s => s ? { ...s, messages: [...s.messages, userMsg, assistantMsg] } : s);
    this.sending.set(true);

    this.chatSessionService.sendMessage(session.id, input)
      .pipe(
        finalize(() => {
          this.sending.set(false);
          // 清除 isStreaming 标志
          this.activeSession.update(s => s ? {
            ...s,
            messages: s.messages.map(m => m.id === assistantMsg.id ? { ...m, isStreaming: false } : m)
          } : s);
        }),
        takeUntilDestroyed(this.destroyRef)
      )
      .subscribe({
        next: event => {
          if (event.type === 'Content' && event.content && !event.isComplete) {
            this.activeSession.update(s => s ? {
              ...s,
              messages: s.messages.map(m =>
                m.id === assistantMsg.id ? { ...m, content: m.content + event.content } : m
              )
            } : s);
          }
        },
        error: err => {
          this.activeSession.update(s => s ? {
            ...s,
            messages: s.messages.map(m =>
              m.id === assistantMsg.id ? { ...m, content: `错误：${err.message}`, isStreaming: false } : m
            )
          } : s);
        }
      });
  }

  onDeleteSession(sessionId: string) {
    this.chatSessionService.deleteSession(sessionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        this.sessions.update(list => list.filter(s => s.id !== sessionId));
        this.layoutService.workspaceSessions.update(list => list.filter(s => s.id !== sessionId));
        if (this.activeSession()?.id === sessionId) {
          this.activeSession.set(null);
          const remaining = this.sessions();
          if (remaining.length > 0) {
            this.router.navigate(['/workspace/chat', remaining[0].id]);
          } else {
            this.router.navigate(['/workspace/chat']);
          }
        }
      });
  }
}
```

### 8.3 `workspace-chat.html`

```html
<!-- workspace-chat.html -->
<div class="flex flex-col h-full">

  @if (activeSession()) {
    <!-- 配置头 -->
    <app-chat-toolbar
      [session]="activeSession()!"
      [providerGroups]="providerGroups()"
      [availableModels]="availableModels()"
      (sessionChange)="activeSession.set($event)"
    />

    <!-- 消息列表 -->
    <div class="flex-1 overflow-y-auto px-4 py-6 space-y-4 custom-scrollbar">
      @for (message of activeSession()!.messages; track message.id) {
        <app-message-bubble [message]="message" />
      }
    </div>

    <!-- 输入区 -->
    <div class="border-t border-surface-200 dark:border-surface-800 px-4 py-3">
      <div class="flex items-end gap-2 max-w-4xl mx-auto">
        <textarea
          pTextarea
          [autoResize]="true"
          rows="1"
          placeholder="输入消息，Shift+Enter 换行，Enter 发送"
          class="flex-1 resize-none"
          [(ngModel)]="inputText"
          (keydown.enter)="$event.shiftKey ? null : ($event.preventDefault(); onSendMessage(inputText()))"
        ></textarea>
        <p-button
          icon="pi pi-send"
          [loading]="sending()"
          [disabled]="!inputText().trim()"
          (onClick)="onSendMessage(inputText()); inputText.set('')"
        />
      </div>
    </div>

  } @else {
    <!-- 空状态：无会话 -->
    <div class="flex flex-col items-center justify-center h-full gap-4 text-muted-color">
      <i class="pi pi-comments text-5xl"></i>
      <p class="text-lg">还没有会话，点击左侧 + 按钮新建一个吧</p>
    </div>
  }
</div>
```

---

## 十、Widget 组件

### 9.1 `chat-toolbar.ts`（会话配置头）

```typescript
// widgets/chat-toolbar/chat-toolbar.ts
import { ChangeDetectionStrategy, Component, EventEmitter, Output, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';

import { AccountTokenService } from '../../../../../platform/services/account-token-service';
import { ModelOptionOutputDto } from '../../../../../platform/models/model-option.dto';
import { ProviderGroupOutputDto } from '../../../../../platform/models/provider-group.dto';
import { ChatSessionOutputDto, UpdateChatSessionInputDto } from '../../../../models/chat-session.dto';
import { ChatSessionService } from '../../../../services/chat-session-service';

@Component({
  selector: 'app-chat-toolbar',
  standalone: true,
  imports: [FormsModule, SelectModule, InputTextModule],
  templateUrl: './chat-toolbar.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ChatToolbar {
  session = input.required<ChatSessionOutputDto>();
  providerGroups = input.required<ProviderGroupOutputDto[]>();
  availableModels = input.required<ModelOptionOutputDto[]>();

  @Output() readonly sessionChange = new EventEmitter<ChatSessionOutputDto>();

  private readonly chatSessionService = inject(ChatSessionService);

  onConfigChange(field: keyof UpdateChatSessionInputDto, value: unknown) {
    const update: UpdateChatSessionInputDto = { [field]: value };
    this.chatSessionService.updateSession(this.session().id, update).subscribe(updated => {
      this.sessionChange.emit(updated);
    });
  }
}
```

```html
<!-- chat-toolbar.html -->
<div class="flex items-center gap-3 px-4 py-2 border-b border-surface-200 dark:border-surface-800 bg-surface-0 dark:bg-surface-900 shrink-0">
  <span class="font-semibold text-sm flex-1 truncate">{{ session().title }}</span>

  <!-- 分组选择 -->
  <p-select
    [options]="providerGroups()"
    optionLabel="name"
    optionValue="id"
    [ngModel]="session().providerGroupId"
    placeholder="选择分组"
    styleClass="text-sm"
    (ngModelChange)="onConfigChange('providerGroupId', $event)"
  />

  <!-- 模型选择（可编辑） -->
  <p-select
    [options]="availableModels()"
    optionLabel="label"
    optionValue="value"
    [ngModel]="session().modelId"
    [editable]="true"
    placeholder="选择或输入模型"
    styleClass="text-sm w-48"
    (ngModelChange)="onConfigChange('modelId', $event)"
  />
</div>
```

### 9.2 `message-bubble.ts`（消息气泡）

```typescript
// widgets/message-bubble/message-bubble.ts
import { ChangeDetectionStrategy, Component, SecurityContext, inject, input } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { CommonModule } from '@angular/common';
import { ImageModule } from 'primeng/image';
import MarkdownIt from 'markdown-it';

import { ChatMessageOutputDto } from '../../../../models/chat-session.dto';

@Component({
  selector: 'app-message-bubble',
  standalone: true,
  imports: [CommonModule, ImageModule],
  templateUrl: './message-bubble.html',
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class MessageBubble {
  message = input.required<ChatMessageOutputDto>();

  private readonly sanitizer = inject(DomSanitizer);
  private readonly md = new MarkdownIt({ html: false, linkify: true, typographer: true });

  get renderedHtml(): string {
    const raw = this.md.render(this.message().content || '');
    return this.sanitizer.sanitize(SecurityContext.HTML, raw) ?? '';
  }

  get isUser(): boolean {
    return this.message().role === 'user';
  }
}
```

```html
<!-- message-bubble.html -->
<div class="flex gap-3" [class.flex-row-reverse]="isUser">
  <!-- 头像 -->
  <div class="w-8 h-8 rounded-full flex items-center justify-center shrink-0 text-white text-xs font-bold"
    [class.bg-primary-500]="isUser"
    [class.bg-surface-400]="!isUser">
    {{ isUser ? '我' : 'AI' }}
  </div>

  <!-- 内容气泡 -->
  <div class="max-w-[70%] rounded-2xl px-4 py-3 text-sm"
    [class.bg-primary-100]="isUser"
    [class.dark:bg-primary-900]="isUser"
    [class.bg-surface-100]="!isUser"
    [class.dark:bg-surface-800]="!isUser">

    <!-- Markdown 渲染 -->
    <div class="prose prose-sm dark:prose-invert max-w-none"
      [innerHTML]="renderedHtml">
    </div>

    <!-- 流式光标 -->
    @if (message().isStreaming) {
      <span class="inline-block w-1.5 h-4 bg-current animate-pulse ml-0.5 align-middle"></span>
    }

    <!-- 图片附件 -->
    @if (message().attachments?.length) {
      <div class="mt-2 flex flex-wrap gap-2">
        @for (attachment of message().attachments; track attachment.url) {
          @if (attachment.type === 'image') {
            <p-image [src]="attachment.url" [alt]="attachment.fileName ?? 'image'"
              [preview]="true" imageClass="max-w-xs rounded-lg" />
          }
        }
      </div>
    }
  </div>
</div>
```

---

## 十一、后端实现（Phase 2）

### 10.1 Domain 实体

```csharp
// AiRelay.Domain/ChatSessions/Entities/ChatSession.cs
public class ChatSession : FullAuditedAggregateRoot<Guid>
{
    public string Title { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public Guid ProviderGroupId { get; set; }
    public Guid? AccountId { get; set; }      // null = 自动调度
    public string ModelId { get; set; } = string.Empty;

    private readonly List<ChatMessage> _messages = [];
    public IReadOnlyCollection<ChatMessage> Messages => _messages.AsReadOnly();

    public void AddMessage(string role, string content)
    {
        _messages.Add(new ChatMessage(Guid.NewGuid(), Id, role, content));
    }

    public void UpdateConfig(string? title, Guid? providerGroupId, Guid? accountId, string? modelId)
    {
        if (title != null) Title = title;
        if (providerGroupId.HasValue) ProviderGroupId = providerGroupId.Value;
        AccountId = accountId;
        if (modelId != null) ModelId = modelId;
    }
}

// AiRelay.Domain/ChatSessions/Entities/ChatMessage.cs
public class ChatMessage : Entity<Guid>
{
    public Guid SessionId { get; private set; }
    public string Role { get; private set; } = string.Empty;   // "user" | "assistant" | "system"
    public string Content { get; private set; } = string.Empty;
    public DateTime CreationTime { get; private set; } = DateTime.UtcNow;

    public ChatMessage(Guid id, Guid sessionId, string role, string content)
    {
        Id = id;
        SessionId = sessionId;
        Role = role;
        Content = content;
    }
}
```

### 10.2 AppService 接口

```csharp
// AiRelay.Application/ChatSessions/AppServices/IChatSessionAppService.cs
public interface IChatSessionAppService
{
    Task<List<ChatSessionOutputDto>> GetSessionsAsync(Guid userId, CancellationToken ct = default);
    Task<ChatSessionOutputDto> GetSessionAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task<ChatSessionOutputDto> CreateSessionAsync(CreateChatSessionInputDto input, Guid userId, CancellationToken ct = default);
    Task<ChatSessionOutputDto> UpdateSessionAsync(Guid id, UpdateChatSessionInputDto input, Guid userId, CancellationToken ct = default);
    Task DeleteSessionAsync(Guid id, Guid userId, CancellationToken ct = default);
    IAsyncEnumerable<StreamEvent> SendMessageAsync(Guid sessionId, SendChatMessageInputDto input, Guid userId, CancellationToken ct = default);
}
```

### 10.3 Controller SSE 端点

```csharp
// AiRelay.Api/Controllers/ChatSessionController.cs
[Authorize]
[ApiController]
[Route("api/v1/chat-sessions")]
public class ChatSessionController(IChatSessionAppService appService, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<List<ChatSessionOutputDto>> GetSessions(CancellationToken ct)
        => await appService.GetSessionsAsync(currentUser.GetId(), ct);

    [HttpGet("{id:guid}")]
    public async Task<ChatSessionOutputDto> GetSession(Guid id, CancellationToken ct)
        => await appService.GetSessionAsync(id, currentUser.GetId(), ct);

    [HttpPost]
    public async Task<ChatSessionOutputDto> CreateSession([FromBody] CreateChatSessionInputDto input, CancellationToken ct)
        => await appService.CreateSessionAsync(input, currentUser.GetId(), ct);

    [HttpPut("{id:guid}")]
    public async Task<ChatSessionOutputDto> UpdateSession(Guid id, [FromBody] UpdateChatSessionInputDto input, CancellationToken ct)
        => await appService.UpdateSessionAsync(id, input, currentUser.GetId(), ct);

    [HttpDelete("{id:guid}")]
    public async Task DeleteSession(Guid id, CancellationToken ct)
        => await appService.DeleteSessionAsync(id, currentUser.GetId(), ct);

    [HttpPost("{id:guid}/messages")]
    public async Task SendMessage(Guid id, [FromBody] SendChatMessageInputDto input, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await foreach (var @event in appService.SendMessageAsync(id, input, currentUser.GetId(), ct))
        {
            await Response.WriteAsync($"data: {JsonSerializer.Serialize(@event)}\n\n", ct);
            await Response.Body.FlushAsync(ct);
        }
    }
}
```

---

## 十二、依赖项

### 前端新增依赖

```bash
npm install markdown-it
npm install @types/markdown-it --save-dev
```

### Angular 模块新增（workspace-chat.ts imports）

```
CommonModule, FormsModule,
ButtonModule, TextareaModule, SelectModule, ImageModule,
ChatToolbar, MessageBubble
```

---

## 十三、关键约定

| 约定 | 说明 |
|------|------|
| Page 组件无 OnPush | `WorkspaceChatPage` 不使用 `ChangeDetectionStrategy.OnPush` |
| Widget 使用 OnPush | `ChatToolbar`、`MessageBubble` 均使用 OnPush |
| Signal 输入 | Widget 用 `input.required<T>()` 而非 `@Input()` |
| 输出用 readonly | `@Output() readonly xxx = new EventEmitter()` |
| 订阅清理 | `takeUntilDestroyed(this.destroyRef)` 替代 `ngOnDestroy` |
| loading 状态 | `finalize()` 重置 loading signal |
| 会话路由 | `/workspace/chat/:sessionId`，无参数时重定向至第一个会话 |
| Sidebar 会话列表 | 由 `LayoutService.workspaceSessions` signal 驱动，Page 写入 |
| Mock SSE | 通过 `SSE_MOCK_REGISTRY.register(url正则, async generator)` 注册 |
| 后端 DTO | 使用 `record` 类型，Mapster `MapsterProfile` 映射 |
| 并发控制 | `sending` signal 防止重复发送，流式结束后复位 |

---

## 十四、实现顺序建议

### Phase 1（前端 + Mock）

1. `chat-session.dto.ts` — DTO 定义
2. `_mock/data/workspace-chat.ts` — Mock 数据
3. `_mock/api/workspace-chat.ts` — HTTP + SSE Mock 处理器
4. `_mock/index.ts` + `MockInterceptor` — 注册路由
5. `chat-session-service.ts` — 服务层
6. `LayoutService` — 新增 `workspaceSessions` signal
7. `DefaultSidebar` — 扩展子菜单支持
8. `workspace.routes.ts` — 更新路由
9. `MessageBubble` widget — 消息气泡组件
10. `ChatToolbar` widget — 会话配置头组件
11. `WorkspaceChatPage` — 页面主组件

### Phase 2（后端）

1. Domain 实体 `ChatSession` + `ChatMessage`
2. EF Core 配置 + 迁移
3. 仓储接口 + 实现
4. AppService 接口 + 实现（含 SSE `IAsyncEnumerable`）
5. Mapster Profile
6. Controller + 路由注册
7. 前端服务切换（移除 Mock 前缀，指向真实 API）
