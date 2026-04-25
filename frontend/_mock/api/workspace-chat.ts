import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { ChatMessageOutputDto, ChatModelOptionOutputDto, ChatSessionOutputDto, CreateChatSessionInputDto, UpdateChatSessionInputDto } from '../../src/app/features/workspace/models/chat-session.dto';
import { MockException, MockRequest } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { MockChatSession, WORKSPACE_CHAT_MODEL_OPTIONS, WORKSPACE_CHAT_SESSIONS, createWorkspaceMockStream, resolveWorkspaceMockAnswer } from '../data/workspace-chat';
import { getVisibleGroupsForCurrentUser } from './provider-group';
import { getCurrentUserId, getUserByAuthHeader } from '../utils/current-user';

let sessions = structuredClone(WORKSPACE_CHAT_SESSIONS) as MockChatSession[];

function toSessionOutput(session: MockChatSession): ChatSessionOutputDto {
  return {
    id: session.id,
    title: session.title,
    providerGroupId: session.providerGroupId,
    modelId: session.modelId,
    accountId: session.accountId,
    creationTime: session.creationTime,
    lastMessageTime: session.lastMessageTime,
    lastMessagePreview: session.lastMessagePreview,
    messageCount: session.messages.length
  };
}

function sortSessions(items: MockChatSession[]) {
  return [...items].sort((a, b) => {
    const timeA = new Date(a.lastMessageTime ?? a.creationTime).getTime();
    const timeB = new Date(b.lastMessageTime ?? b.creationTime).getTime();
    return timeB - timeA;
  });
}

function getSessions(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  return sortSessions(sessions.filter(item => item.userId === currentUserId)).map(toSessionOutput);
}

function getSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const session = sessions.find(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (!session) {
    throw new MockException(404, { message: '会话不存在' });
  }

  return toSessionOutput(session);
}

function getMessagePagedList(req: MockRequest): PagedResultDto<ChatMessageOutputDto> {
  const currentUserId = getCurrentUserId(req);
  const session = sessions.find(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (!session) {
    throw new MockException(404, { message: '会话不存在' });
  }

  const limit = Math.max(1, Math.min(100, Number(req.queryParams['limit'] ?? 30)));
  const cursorMessageId = String(req.queryParams['cursorMessageId'] ?? '').trim();
  const sortedMessages = [...session.messages].sort((a, b) => new Date(a.creationTime).getTime() - new Date(b.creationTime).getTime());

  let endIndex = sortedMessages.length;
  if (cursorMessageId) {
    const cursorIndex = sortedMessages.findIndex(item => item.id === cursorMessageId);
    endIndex = cursorIndex >= 0 ? cursorIndex : 0;
  }

  const availableItems = sortedMessages.slice(0, endIndex);
  const startIndex = Math.max(0, availableItems.length - limit);
  const items = availableItems.slice(startIndex);

  return {
    totalCount: availableItems.length,
    items
  };
}

function createSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const input = req.body as CreateChatSessionInputDto;
  const now = new Date().toISOString();
  const visibleGroupIds = new Set(getVisibleGroupsForCurrentUser(req).map(group => group.id));

  if (input.providerGroupId && !visibleGroupIds.has(input.providerGroupId)) {
    throw new MockException(404, { message: '资源池不存在' });
  }

  const session: MockChatSession = {
    userId: currentUserId,
    id: crypto.randomUUID(),
    title: input.title?.trim() || '新会话',
    providerGroupId: input.providerGroupId,
    modelId: input.modelId,
    accountId: input.accountId,
    creationTime: now,
    lastMessageTime: now,
    lastMessagePreview: undefined,
    messages: []
  };

  sessions = [session, ...sessions];
  return toSessionOutput(session);
}

function updateSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const index = sessions.findIndex(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (index === -1) {
    throw new MockException(404, { message: '会话不存在' });
  }

  const current = sessions[index];
  const input = req.body as UpdateChatSessionInputDto;
  const visibleGroupIds = new Set(getVisibleGroupsForCurrentUser(req).map(group => group.id));
  if (input.providerGroupId && !visibleGroupIds.has(input.providerGroupId)) {
    throw new MockException(404, { message: '资源池不存在' });
  }
  const updated: MockChatSession = {
    ...current,
    title: input.title ?? current.title,
    providerGroupId: input.useAutoProviderGroup ? undefined : (input.providerGroupId === undefined ? current.providerGroupId : input.providerGroupId),
    modelId: input.modelId ?? current.modelId,
    accountId: input.accountId ?? current.accountId
  };

  sessions[index] = updated;
  return toSessionOutput(updated);
}

function deleteSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const index = sessions.findIndex(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (index === -1) {
    throw new MockException(404, { message: '会话不存在' });
  }

  sessions.splice(index, 1);
  sessions = [...sessions];
  return { success: true };
}

function getModelOptions(req: MockRequest): ChatModelOptionOutputDto[] {
  const visibleGroupIds = new Set(getVisibleGroupsForCurrentUser(req).map(group => group.id));
  const providerGroupId = String(req.queryParams['providerGroupId'] ?? '').trim();
  if (!providerGroupId) {
    return WORKSPACE_CHAT_MODEL_OPTIONS.all.filter(option => !option.providerGroupId || visibleGroupIds.has(option.providerGroupId));
  }

  if (!visibleGroupIds.has(providerGroupId)) {
    throw new MockException(404, { message: '资源池不存在' });
  }

  return (WORKSPACE_CHAT_MODEL_OPTIONS.byGroup[providerGroupId] ?? []).filter(option => !option.providerGroupId || visibleGroupIds.has(option.providerGroupId));
}

function buildTitle(input: string) {
  const normalized = input.replace(/\s+/g, ' ').trim();
  return normalized.length > 20 ? `${normalized.slice(0, 20)}...` : normalized;
}

SSE_MOCK_REGISTRY.register('POST', /\/api\/v1\/chat-sessions\/[^/]+\/messages$/, (body?: unknown, context?: { url: string; headers?: Headers }) => {
  const sessionId = context?.url.match(/\/api\/v1\/chat-sessions\/([^/]+)\/messages$/)?.[1];
  const currentUserId = getUserByAuthHeader(context?.headers?.get('Authorization')).id;
  const prompt = typeof body === 'object' && body && 'content' in body ? String((body as { content?: string }).content ?? '') : '';
  const answer = resolveWorkspaceMockAnswer(prompt);

  if (!sessionId) {
    throw new MockException(400, { message: '会话标识缺失' });
  }

  const targetSession = sessions.find(session => session.id === sessionId && session.userId === currentUserId);
  if (!targetSession) {
    throw new MockException(404, { message: '会话不存在或无权访问' });
  }

  sessions = sessions.map(session => {
    if (session.id !== sessionId) {
      return session;
    }

    const now = new Date().toISOString();
    return {
      ...session,
      title: session.title === '新会话' ? buildTitle(prompt) : session.title,
      lastMessageTime: now,
      lastMessagePreview: answer,
      messages: [
        ...session.messages,
        {
          id: crypto.randomUUID(),
          sessionId,
          role: 'user',
          content: prompt,
          creationTime: now
        },
        {
          id: crypto.randomUUID(),
          sessionId,
          role: 'assistant',
          content: answer,
          creationTime: new Date(Date.now() + 1000).toISOString()
        }
      ]
    };
  });

  return createWorkspaceMockStream(prompt);
});

export const WORKSPACE_CHAT_API = {
  'GET /api/v1/chat-sessions': (req: MockRequest) => getSessions(req),
  'GET /api/v1/chat-sessions/model-options': (req: MockRequest) => getModelOptions(req),
  'GET /api/v1/chat-sessions/:id': (req: MockRequest) => getSession(req),
  'GET /api/v1/chat-sessions/:id/messages': (req: MockRequest) => getMessagePagedList(req),
  'POST /api/v1/chat-sessions': (req: MockRequest) => createSession(req),
  'PUT /api/v1/chat-sessions/:id': (req: MockRequest) => updateSession(req),
  'DELETE /api/v1/chat-sessions/:id': (req: MockRequest) => deleteSession(req)
};
