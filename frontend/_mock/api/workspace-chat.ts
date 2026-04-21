import { ChatSessionOutputDto, CreateChatSessionInputDto, UpdateChatSessionInputDto } from '../../src/app/features/workspace/models/chat-session.dto';
import { MockException, MockRequest } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { MockChatSession, WORKSPACE_CHAT_MODEL_OPTIONS, WORKSPACE_CHAT_SESSIONS, createWorkspaceMockStream, resolveWorkspaceMockAnswer } from '../data/workspace-chat';
import { getCurrentUserId, getUserByAuthHeader } from '../utils/current-user';

let sessions = structuredClone(WORKSPACE_CHAT_SESSIONS) as MockChatSession[];

function sortSessions(items: MockChatSession[]) {
  return [...items].sort((a, b) => {
    const timeA = new Date(a.lastMessageTime ?? a.creationTime).getTime();
    const timeB = new Date(b.lastMessageTime ?? b.creationTime).getTime();
    return timeB - timeA;
  });
}

function getSessions(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  return sortSessions(sessions.filter(item => item.userId === currentUserId)).map(({ userId: _userId, ...item }) => item);
}

function getSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const session = sessions.find(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (!session) {
    throw new MockException(404, { message: '会话不存在' });
  }

  const { userId: _userId, ...result } = session;
  return result;
}

function createSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const input = req.body as CreateChatSessionInputDto;
  const now = new Date().toISOString();

  const session: MockChatSession = {
    userId: currentUserId,
    id: crypto.randomUUID(),
    title: input.title?.trim() || '新会话',
    providerGroupId: input.providerGroupId,
    modelId: input.modelId,
    accountId: input.accountId,
    creationTime: now,
    lastMessageTime: now,
    messages: []
  };

  sessions = [session, ...sessions];
  const { userId: _userId, ...result } = session;
  return result;
}

function updateSession(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  const index = sessions.findIndex(item => item.id === req.params['id'] && item.userId === currentUserId);
  if (index === -1) {
    throw new MockException(404, { message: '会话不存在' });
  }

  const current = sessions[index];
  const input = req.body as UpdateChatSessionInputDto;
  const updated: MockChatSession = {
    ...current,
    title: input.title ?? current.title,
    providerGroupId: input.providerGroupId ?? current.providerGroupId,
    modelId: input.modelId ?? current.modelId,
    accountId: input.accountId ?? current.accountId
  };

  sessions[index] = updated;
  const { userId: _userId, ...result } = updated;
  return result;
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

function getModelOptions() {
  return WORKSPACE_CHAT_MODEL_OPTIONS;
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
  'GET /api/v1/chat-sessions/model-options': () => getModelOptions(),
  'GET /api/v1/chat-sessions/:id': (req: MockRequest) => getSession(req),
  'POST /api/v1/chat-sessions': (req: MockRequest) => createSession(req),
  'PUT /api/v1/chat-sessions/:id': (req: MockRequest) => updateSession(req),
  'DELETE /api/v1/chat-sessions/:id': (req: MockRequest) => deleteSession(req)
};
