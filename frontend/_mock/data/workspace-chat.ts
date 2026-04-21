import { ChatModelOptionOutputDto, ChatSessionOutputDto } from '../../src/app/features/workspace/models/chat-session.dto';

export interface MockChatSession extends ChatSessionOutputDto {
  userId: string;
}

export const WORKSPACE_CHAT_MODEL_OPTIONS: ChatModelOptionOutputDto[] = [
  { label: 'Gemini 2.5 Pro', value: 'gemini-2.5-pro' },
  { label: 'Gemini 2.5 Flash', value: 'gemini-2.5-flash' },
  { label: 'Claude Sonnet 4.6', value: 'claude-sonnet-4-6' },
  { label: 'GPT-4.1', value: 'gpt-4.1' },
  { label: 'GPT-4o', value: 'gpt-4o' }
];

export const WORKSPACE_CHAT_SESSIONS: MockChatSession[] = [
  {
    id: 'session-1',
    userId: '00000000-0000-0000-0000-000000000001',
    title: 'Gemini 2.5 Pro 方案讨论',
    providerGroupId: 'group-default',
    modelId: 'gemini-2.5-pro',
    creationTime: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 9).toISOString(),
    messages: [
      {
        id: 'session-1-msg-1',
        sessionId: 'session-1',
        role: 'user',
        content: '帮我整理一下工作区聊天页面的核心布局和交互重点。',
        creationTime: new Date(Date.now() - 1000 * 60 * 11).toISOString()
      },
      {
        id: 'session-1-msg-2',
        sessionId: 'session-1',
        role: 'assistant',
        content:
          '可以把页面拆成三个稳定区域：会话列表、上下文头部、消息流与输入区。这样首屏结构清晰，后续扩展日志和订阅页也不会互相干扰。',
        creationTime: new Date(Date.now() - 1000 * 60 * 9).toISOString()
      }
    ]
  },
  {
    id: 'session-2',
    userId: '00000000-0000-0000-0000-000000000001',
    title: 'Claude Code Review',
    providerGroupId: 'group-default',
    modelId: 'claude-sonnet-4-6',
    creationTime: new Date(Date.now() - 1000 * 60 * 60 * 6).toISOString(),
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 42).toISOString(),
    messages: [
      {
        id: 'session-2-msg-1',
        sessionId: 'session-2',
        role: 'user',
        content: '这段 Angular 信号状态管理有没有明显问题？',
        creationTime: new Date(Date.now() - 1000 * 60 * 45).toISOString()
      },
      {
        id: 'session-2-msg-2',
        sessionId: 'session-2',
        role: 'assistant',
        content:
          '重点先看状态来源是否单一、是否把路由态和视图态混在一起，以及流式消息结束后有没有正确清理临时字段。',
        creationTime: new Date(Date.now() - 1000 * 60 * 42).toISOString()
      }
    ]
  },
  {
    id: 'session-3',
    userId: '00000000-0000-0000-0000-000000000002',
    title: '工作区指标复盘',
    providerGroupId: 'group-openai-vip',
    modelId: 'gpt-4o',
    creationTime: new Date(Date.now() - 1000 * 60 * 60 * 12).toISOString(),
    lastMessageTime: new Date(Date.now() - 1000 * 60 * 18).toISOString(),
    messages: [
      {
        id: 'session-3-msg-1',
        sessionId: 'session-3',
        role: 'user',
        content: '帮我总结一下最近两周工作区的用量走势。',
        creationTime: new Date(Date.now() - 1000 * 60 * 22).toISOString()
      },
      {
        id: 'session-3-msg-2',
        sessionId: 'session-3',
        role: 'assistant',
        content: '可以重点看请求量、模型分布、最高频 API Key 和失败率，避免只看单日调用次数。',
        creationTime: new Date(Date.now() - 1000 * 60 * 18).toISOString()
      }
    ]
  }
];

export function resolveWorkspaceMockAnswer(prompt: string) {
  return prompt.includes('订阅') || prompt.includes('subscription')
    ? '建议把“我的订阅”做成卡片流，每张卡片只承载状态、密钥、今日用量和到期时间四类信息，避免后台表格式噪音。'
    : prompt.includes('日志') || prompt.includes('usage')
      ? '使用日志页应保留筛选和状态标签，但字段只展示普通用户关心的时间、模型、Token、费用、耗时和状态。'
      : '这版工作区页面建议保持现有系统风格，用大圆角、轻边框和低噪音层级组织内容，主交互集中在聊天工作面本身。';
}

function chunkText(text: string, size = 6) {
  const events: Array<{ type: 'Content'; content?: string; isComplete?: boolean }> = [];
  let index = 0;

  while (index < text.length) {
    events.push({
      type: 'Content',
      content: text.slice(index, index + size)
    });
    index += size;
  }

  events.push({
    type: 'Content',
    isComplete: true
  });

  return events;
}

export function createWorkspaceMockStream(prompt: string) {
  return chunkText(resolveWorkspaceMockAnswer(prompt), 5);
}
