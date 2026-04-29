import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { ChatMessageOutputDto, ChatModelOptionOutputDto, ChatSessionOutputDto, CreateChatSessionInputDto, UpdateChatSessionInputDto } from '../../src/app/features/workspace/models/chat-session.dto';
import { MockException, MockRequest } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { ACCOUNT_TOKENS, ANTIGRAVITY_MODELS, AVAILABLE_MODELS } from '../data/account-token';
import { MockChatSession, WORKSPACE_CHAT_SESSIONS, createWorkspaceMockStream, resolveWorkspaceMockAnswer, resolveWorkspaceMockReasoning } from '../data/workspace-chat';
import { getVisibleGroupsForCurrentUser } from './provider-group';
import { getCurrentUserId, getUserByAuthHeader } from '../utils/current-user';
import { Provider } from '../../src/app/shared/models/provider.enum';
import { ModelVendor } from '../../src/app/shared/models/model-vendor.enum';

let sessions = structuredClone(WORKSPACE_CHAT_SESSIONS) as MockChatSession[];

const DEFAULT_CATALOG_VENDORS: Record<Provider, ModelVendor[]> = {
  [Provider.Gemini]: [ModelVendor.Google],
  [Provider.Claude]: [ModelVendor.Anthropic],
  [Provider.OpenAI]: [ModelVendor.OpenAI],
  [Provider.Antigravity]: [],
  [Provider.OpenAICompatible]: [ModelVendor.Qwen, ModelVendor.Moonshot, ModelVendor.DeepSeek, ModelVendor.MiniMax, ModelVendor.Zhipu, ModelVendor.Jimeng]
};

function getBaselineModels(provider: Provider) {
  if (provider === Provider.Antigravity) {
    return ANTIGRAVITY_MODELS;
  }

  const vendors = DEFAULT_CATALOG_VENDORS[provider] ?? [];
  return vendors.flatMap(vendor => AVAILABLE_MODELS[vendor] ?? []);
}

function isPatternMatch(text: string, pattern: string) {
  const parts = pattern.split('*');
  let pos = 0;

  for (let i = 0; i < parts.length; i++) {
    const part = parts[i];
    if (!part) {
      continue;
    }

    const idx = text.toLowerCase().indexOf(part.toLowerCase(), pos);
    if (idx < 0) {
      return false;
    }

    if (i === 0 && idx !== 0) {
      return false;
    }

    pos = idx + part.length;
  }

  return !parts[parts.length - 1] || pos === text.length;
}

function tryResolveMappingSource(modelId: string, mapping: Record<string, string>) {
  const exact = mapping[modelId];
  if (exact) {
    return exact;
  }

  const match = Object.entries(mapping)
    .filter(([key]) => key.includes('*') && isPatternMatch(modelId, key))
    .sort((a, b) => b[0].length - a[0].length)[0];

  if (!match) {
    return null;
  }

  const [key, value] = match;
  if (value.endsWith('*')) {
    const suffix = modelId.slice(key.length - 1);
    return `${value.slice(0, -1)}${suffix}`;
  }

  return value;
}

function getMockUpstreamModels(account: any) {
  if (account.provider === Provider.OpenAICompatible) {
    return ['Qwen/Qwen3.6-plus', 'Qwen/Qwen3.5-plus', 'deepseek-v4-pro', 'kimi-k2.6'];
  }

  if (account.provider === Provider.OpenAI) {
    return ['gpt-5.5', 'gpt-5.4', 'gpt-5.4-mini'];
  }

  if (account.provider === Provider.Gemini) {
    return ['gemini-3.1-pro-preview', 'gemini-2.5-pro', 'gemini-2.5-flash', 'gemma-4-31B-it'];
  }

  if (account.provider === Provider.Claude) {
    return ['claude-opus-4-7', 'claude-opus-4-6', 'claude-sonnet-4-6'];
  }

  return null;
}

function isAccountAvailable(account: any) {
  return account.isActive && account.status !== 'Error' && account.status !== 'RateLimited';
}

function isModelRateLimited(account: any, modelKey: string) {
  return Array.isArray(account.limitedModels)
    && account.limitedModels.some((item: { modelKey: string }) => item.modelKey?.toLowerCase() === modelKey.toLowerCase());
}

function resolveChatCandidateModels(account: any) {
  const catalogModels = getBaselineModels(account.provider);

  if (Array.isArray(account.modelWhites) && account.modelWhites.length > 0) {
    return catalogModels.filter(model => account.modelWhites.some((pattern: string) => isPatternMatch(model.value, pattern)));
  }

  if (account.modelMapping && Object.keys(account.modelMapping).length > 0) {
    return catalogModels.filter(model => tryResolveMappingSource(model.value, account.modelMapping) !== null);
  }

  const upstreamModels = getMockUpstreamModels(account);
  if (upstreamModels?.length) {
    const upstreamSet = new Set(upstreamModels.map(item => item.toLowerCase()));
    return catalogModels.filter(model => {
      const mapped = tryResolveMappingSource(model.value, account.modelMapping ?? {}) ?? model.value;
      return upstreamSet.has(mapped.toLowerCase());
    });
  }

  return catalogModels;
}

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
  const visibleGroups = getVisibleGroupsForCurrentUser(req);
  const visibleGroupIds = new Set(visibleGroups.map(group => group.id));
  const groupNameLookup = new Map(visibleGroups.map(group => [group.id, group.name]));
  const providerGroupId = String(req.queryParams['providerGroupId'] ?? '').trim();

  if (providerGroupId && !visibleGroupIds.has(providerGroupId)) {
    throw new MockException(404, { message: '资源池不存在' });
  }

  const targetAccounts = ACCOUNT_TOKENS.filter(account =>
    isAccountAvailable(account)
    && account.providerGroupIds.some((groupId: string) => visibleGroupIds.has(groupId))
    && (!providerGroupId || account.providerGroupIds.includes(providerGroupId)));

  const result = new Map<string, ChatModelOptionOutputDto>();
  for (const account of targetAccounts) {
    const resolvedGroupId = providerGroupId ?? account.providerGroupIds.find((groupId: string) => visibleGroupIds.has(groupId));
    if (!resolvedGroupId) {
      continue;
    }

    for (const model of resolveChatCandidateModels(account)) {
      const routedModel = tryResolveMappingSource(model.value, account.modelMapping ?? {}) ?? model.value;
      if (isModelRateLimited(account, routedModel) || result.has(model.value.toLowerCase())) {
        continue;
      }

      result.set(model.value.toLowerCase(), {
        label: model.label,
        value: model.value,
        category: model.category ?? 'Chat',
        vendor: model.vendor,
        providerGroupId: resolvedGroupId,
        providerGroupName: groupNameLookup.get(resolvedGroupId) ?? resolvedGroupId
      });
    }
  }

  return Array.from(result.values());
}

function buildTitle(input: string) {
  const normalized = input.replace(/\s+/g, ' ').trim();
  return normalized.length > 20 ? `${normalized.slice(0, 20)}...` : normalized;
}

SSE_MOCK_REGISTRY.register('POST', /\/api\/v1\/chat-sessions\/[^/]+\/messages$/, (body?: unknown, context?: { url: string; headers?: Headers }) => {
  const sessionId = context?.url.match(/\/api\/v1\/chat-sessions\/([^/]+)\/messages$/)?.[1];
  const currentUserId = getUserByAuthHeader(context?.headers?.get('Authorization')).id;
  const prompt = typeof body === 'object' && body && 'content' in body ? String((body as { content?: string }).content ?? '') : '';
  const reasoning = resolveWorkspaceMockReasoning(prompt);
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
          reasoningContent: reasoning,
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
