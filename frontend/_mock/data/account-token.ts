import {
  AccountStatus,
  AccountTokenMetricsOutputDto,
  AccountTokenOutputDto
} from '../../src/app/features/platform/models/account-token.dto';
import { ModelOptionOutputDto } from '../../src/app/features/platform/models/model-option.dto';
import { ProviderPlatform } from '../../src/app/shared/models/provider-platform.enum';

export const ACCOUNT_TOKENS: AccountTokenOutputDto[] = [
  {
    id: '1',
    name: 'gemini-account-01@example.com',
    platform: ProviderPlatform.GEMINI_OAUTH,
    baseUrl: '',
    description: '主要测试账号，用于日常开发调试。',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: 3500,
    tokenObtainedTime: new Date().toISOString(),
    creationTime: '2023-12-01T10:00:00Z',
    usageToday: 15420,
    usageTotal: 450120,
    fullToken: '1//0gMo...Z890',
    successRate: 99.8,
    maxConcurrency: 10,
    currentConcurrency: 3
  },
  {
    id: '2',
    name: 'gemini-account-02@example.com',
    platform: ProviderPlatform.GEMINI_OAUTH,
    baseUrl: '',
    description: '',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: 2400,
    tokenObtainedTime: new Date().toISOString(),
    creationTime: '2023-12-01T10:00:00Z',
    usageToday: 8900,
    usageTotal: 120500,
    fullToken: '1//0gMo...4321',
    successRate: 98.5,
    maxConcurrency: 5,
    currentConcurrency: 1
  },
  {
    id: '3',
    name: 'gemini-apikey-backup@example.com',
    platform: ProviderPlatform.GEMINI_APIKEY,
    extraProperties: { project_id: 'example-project-101' },
    baseUrl: '',
    description: '备用 Key，流量限制较严。',
    isActive: false,
    status: AccountStatus.RateLimited,
    statusDescription: '当前账号已触发限流',
    rateLimitDurationSeconds: 3600,
    lockedUntil: new Date(Date.now() + 1800000).toISOString(), // 30 minutes from now
    expiresIn: null,
    tokenObtainedTime: '2023-12-10T09:30:00Z',
    creationTime: '2023-12-10T09:30:00Z',
    usageToday: 0,
    usageTotal: 12400,
    fullToken: 'AIzaSyM...GHIJ',
    successRate: 0,
    maxConcurrency: 2,
    currentConcurrency: 0
  },
  {
    id: '4',
    name: 'claude-relay-01',
    platform: ProviderPlatform.CLAUDE_APIKEY,
    baseUrl: 'https://api.example-relay.com/',
    description: '对接第三方聚合网关。',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: null,
    tokenObtainedTime: '2024-01-05T08:00:00Z',
    creationTime: '2024-01-05T08:00:00Z',
    usageToday: 420,
    usageTotal: 2100,
    fullToken: 'sk-mock...3456',
    successRate: 100,
    maxConcurrency: 0,
    currentConcurrency: 12
  },
  {
    id: '5',
    name: 'claude-relay-02',
    platform: ProviderPlatform.CLAUDE_APIKEY,
    baseUrl: 'https://api.example-relay.com/',
    description: '',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: null,
    tokenObtainedTime: '2024-02-01T11:11:00Z',
    creationTime: '2024-02-01T11:11:00Z',
    usageToday: 3350,
    usageTotal: 50120,
    fullToken: 'sk-mock...4321',
    successRate: 99.1,
    maxConcurrency: 20,
    currentConcurrency: 5
  },
  {
    id: '6',
    name: 'openai-standard',
    platform: ProviderPlatform.OPENAI_OAUTH,
    extraProperties: { project_id: 'org-abc12345', chatgpt_account_id: 'fake-uuid-12345' },
    baseUrl: '',
    description: '标准企业账号，用于 GPT-4 模型调用。',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: null,
    tokenObtainedTime: '2024-03-01T09:00:00Z',
    creationTime: '2024-03-01T09:00:00Z',
    usageToday: 12500,
    usageTotal: 340000,
    fullToken: 'sk-proj...0q',
    successRate: 99.9,
    maxConcurrency: 50,
    currentConcurrency: 45
  },
  {
    id: '7',
    name: 'openai-dev',
    platform: ProviderPlatform.OPENAI_APIKEY,
    baseUrl: 'https://api.openai.com/v1',
    description: '开发测试用 Key',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: null,
    tokenObtainedTime: '2024-03-15T14:20:00Z',
    creationTime: '2024-03-15T14:20:00Z',
    usageToday: 450,
    usageTotal: 2200,
    fullToken: 'sk-dev-...e123',
    successRate: 95.0,
    maxConcurrency: 3,
    currentConcurrency: 0
  },
  {
    id: '8',
    name: 'antigravity-prod-01',
    platform: ProviderPlatform.ANTIGRAVITY,
    extraProperties: { project_id: 'agentrouter-main-2025' },
    baseUrl: 'https://cloudcode-pa.googleapis.com',
    description: 'Antigravity 生产主账号，支持 Gemini 3 系列和 Claude 4.5 思维模型',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: 3200,
    tokenObtainedTime: new Date().toISOString(),
    creationTime: '2024-04-01T08:00:00Z',
    usageToday: 28500,
    usageTotal: 1250000,
    fullToken: '1//0gAn...WXYZ',
    successRate: 99.2,
    maxConcurrency: 100,
    currentConcurrency: 15
  },
  {
    id: '9',
    name: 'antigravity-backup',
    platform: ProviderPlatform.ANTIGRAVITY,
    extraProperties: { project_id: 'backup-project-001' },
    baseUrl: 'https://cloudcode-pa.googleapis.com',
    description: '备用账号，用于流量溢出和故障转移',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: 2800,
    tokenObtainedTime: new Date(Date.now() - 600000).toISOString(), // 10 minutes ago
    creationTime: '2024-04-10T12:30:00Z',
    usageToday: 12300,
    usageTotal: 580000,
    fullToken: '1//0gBa...DCBA',
    successRate: 98.7,
    maxConcurrency: 100,
    currentConcurrency: 8
  },
  {
    id: '10',
    name: 'antigravity-thinking',
    platform: ProviderPlatform.ANTIGRAVITY,
    extraProperties: { project_id: 'thinking-models-2025' },
    baseUrl: '',
    description: '专用于思维模型（Thinking Models）的账号，支持深度推理任务',
    isActive: true,
    status: AccountStatus.Normal,
    expiresIn: 3600,
    tokenObtainedTime: new Date(Date.now() - 300000).toISOString(), // 5 minutes ago
    creationTime: '2024-04-15T09:15:00Z',
    usageToday: 8900,
    usageTotal: 320000,
    fullToken: '1//0gTh...7890',
    successRate: 99.5,
    maxConcurrency: 10,
    currentConcurrency: 10
  },
  {
    id: '11',
    name: 'antigravity-dev',
    platform: ProviderPlatform.ANTIGRAVITY,
    extraProperties: { project_id: 'dev-testing-env' },
    baseUrl: 'https://cloudcode-pa.googleapis.com',
    description: '开发测试环境专用账号',
    isActive: false,
    status: AccountStatus.Error,
    statusDescription: '认证失败 (401)，Token 可能已过期',
    expiresIn: 0,
    tokenObtainedTime: new Date(Date.now() - 7200000).toISOString(), // 2 hours ago
    creationTime: '2024-04-20T14:45:00Z',
    usageToday: 0,
    usageTotal: 45000,
    fullToken: '1//0gDe...MPLE',
    successRate: 0,
    maxConcurrency: 5,
    currentConcurrency: 0
  }
];

export const ACCOUNT_METRICS: AccountTokenMetricsOutputDto = {
  totalAccounts: 11,
  activeAccounts: 9,
  disabledAccounts: 2, // id:3 (RateLimited), id:11 (Error)
  expiringAccounts: 1, // id:11 token expired

  totalUsageToday: 76320,
  usageGrowthRate: 12.3,

  averageSuccessRate: 90.8,
  abnormalRequests24h: 28,

  rotationWarnings: 1 // id:11 needs token refresh
};

export const AVAILABLE_MODELS: Record<ProviderPlatform, ModelOptionOutputDto[]> = {
  [ProviderPlatform.GEMINI_OAUTH]: [
    { label: 'Gemini 3.1 Flash Preview', value: 'gemini-3.1-flash-preview' },
    { label: 'Gemini 3.1 Pro Preview', value: 'gemini-3.1-pro-preview' },
    { label: 'Gemini 3.0 Flash Preview', value: 'gemini-3-flash-preview' },
    { label: 'Gemini 3.0 Pro Preview', value: 'gemini-3-pro-preview' },
    { label: 'Gemini 2.5 Flash', value: 'gemini-2.5-flash' },
    { label: 'Gemini 2.5 Pro', value: 'gemini-2.5-pro' },
    { label: 'Gemini 2.0 Flash', value: 'gemini-2.0-flash' }
  ],
  [ProviderPlatform.GEMINI_APIKEY]: [
    { label: 'Gemini 3.1 Flash Preview', value: 'gemini-3.1-flash-preview' },
    { label: 'Gemini 3.1 Pro Preview', value: 'gemini-3.1-pro-preview' },
    { label: 'Gemini 3.0 Flash Preview', value: 'gemini-3-flash-preview' },
    { label: 'Gemini 3.0 Pro Preview', value: 'gemini-3-pro-preview' },
    { label: 'Gemini 2.5 Flash', value: 'gemini-2.5-flash' },
    { label: 'Gemini 2.5 Pro', value: 'gemini-2.5-pro' },
    { label: 'Gemini 2.0 Flash', value: 'gemini-2.0-flash' }
  ],
  [ProviderPlatform.CLAUDE_OAUTH]: [
    { label: 'Claude Opus 4.6', value: 'claude-opus-4-6' },
    { label: 'Claude Sonnet 4.6', value: 'claude-sonnet-4-6' },
    { label: 'Claude Opus 4.5', value: 'claude-opus-4-5-20251101' },
    { label: 'Claude Sonnet 4.5', value: 'claude-sonnet-4-5-20250929' },
    { label: 'Claude Haiku 4.5', value: 'claude-haiku-4-5-20251001' }
  ],
  [ProviderPlatform.CLAUDE_APIKEY]: [
    { label: 'Claude Opus 4.6', value: 'claude-opus-4-6' },
    { label: 'Claude Sonnet 4.6', value: 'claude-sonnet-4-6' },
    { label: 'Claude Opus 4.5', value: 'claude-opus-4-5-20251101' },
    { label: 'Claude Sonnet 4.5', value: 'claude-sonnet-4-5-20250929' },
    { label: 'Claude Haiku 4.5', value: 'claude-haiku-4-5-20251001' }
  ],
  [ProviderPlatform.OPENAI_OAUTH]: [
    { label: 'GPT-5.4', value: 'gpt-5.4' },
    { label: 'GPT-5.3', value: 'gpt-5.3' },
    { label: 'GPT-5.3 Codex', value: 'gpt-5.3-codex' },
    { label: 'GPT-5.2', value: 'gpt-5.2' },
    { label: 'GPT-5.2 Codex', value: 'gpt-5.2-codex' },
    { label: 'GPT-5.1', value: 'gpt-5.1' },
    { label: 'GPT-5.1 Codex Max', value: 'gpt-5.1-codex-max' },
    { label: 'GPT-5.1 Codex', value: 'gpt-5.1-codex' },
    { label: 'GPT-5.1 Codex Mini', value: 'gpt-5.1-codex-mini' },
    { label: 'GPT-5', value: 'gpt-5' }
  ],
  [ProviderPlatform.OPENAI_APIKEY]: [
    { label: 'GPT-5.4', value: 'gpt-5.4' },
    { label: 'GPT-5.3', value: 'gpt-5.3' },
    { label: 'GPT-5.3 Codex', value: 'gpt-5.3-codex' },
    { label: 'GPT-5.2', value: 'gpt-5.2' },
    { label: 'GPT-5.2 Codex', value: 'gpt-5.2-codex' },
    { label: 'GPT-5.1', value: 'gpt-5.1' },
    { label: 'GPT-5.1 Codex Max', value: 'gpt-5.1-codex-max' },
    { label: 'GPT-5.1 Codex', value: 'gpt-5.1-codex' },
    { label: 'GPT-5.1 Codex Mini', value: 'gpt-5.1-codex-mini' },
    { label: 'GPT-5', value: 'gpt-5' }
  ],
  [ProviderPlatform.ANTIGRAVITY]: [
    { label: 'Gemini 3.1 Pro High', value: 'gemini-3.1-pro-high' },
    { label: 'Gemini 3.1 Pro Low', value: 'gemini-3.1-pro-low' },
    { label: 'Gemini 3 Flash', value: 'gemini-3-flash' },
    { label: 'Gemini 3 Pro (Image)', value: 'gemini-3-pro-image' },
    { label: 'Claude 4.6 Opus (Thinking)', value: 'claude-opus-4-6-thinking' },
    { label: 'Claude 4.6 Sonnet', value: 'claude-sonnet-4-6' },
    { label: 'Claude 4.6 Sonnet (Thinking)', value: 'claude-sonnet-4-6-thinking' }
  ]
};

// Mock streaming chat responses for model test
// 格式对齐 ChatStreamEvent 接口：{ content?, error?, isComplete? }
export const MOCK_CHAT_STREAM_CHUNKS = [
  { content: 'Hello! ' },
  { content: 'I am a ' },
  { content: 'mock AI ' },
  { content: 'assistant. ' },
  { content: 'This is a ' },
  { content: 'simulated ' },
  { content: 'streaming response.' },
  { isComplete: true }
];
