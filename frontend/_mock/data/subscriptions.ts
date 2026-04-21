import { SubscriptionOutputDto } from '../../src/app/features/platform/models/subscription.dto';
import { RouteProfile } from '../../src/app/shared/models/route-profile.enum';

export interface MockSubscription extends SubscriptionOutputDto {
  userId: string;
}

export const SUBSCRIPTIONS: MockSubscription[] = [
  {
    id: 'sub-001',
    userId: '00000000-0000-0000-0000-000000000001',
    name: '开发环境默认 Key',
    description: '用于本地开发测试',
    secret: 'sk_dev_test_key_123456',
    isActive: true,
    creationTime: '2025-01-01T10:00:00Z',
    usageToday: 150,
    usageTotal: 5000,
    costToday: 0.18,
    costTotal: 6,
    tokensToday: 15000,
    tokensTotal: 500000,
    bindings: [
      {
        priority: 1,
        providerGroupId: 'group-default',
        providerGroupName: 'default',
        creationTime: '2025-01-01T10:00:00Z',
        supportedRouteProfiles: [
          RouteProfile.GeminiBeta,
          RouteProfile.GeminiInternal,
          RouteProfile.OpenAiResponses,
          RouteProfile.OpenAiCodex,
          RouteProfile.ChatCompletions,
          RouteProfile.ClaudeMessages
        ]
      }
    ]
  },
  {
    id: 'sub-002',
    userId: '00000000-0000-0000-0000-000000000001',
    name: '生产环境 - 移动端',
    description: '移动端 APP 专用，限流',
    secret: 'sk_mobile-app_prod_987654',
    isActive: true,
    expiresAt: '2026-12-31T23:59:59Z',
    lastUsedAt: '2026-01-20T08:30:00Z',
    creationTime: '2025-06-15T14:30:00Z',
    usageToday: 8900,
    usageTotal: 250000,
    costToday: 13.35,
    costTotal: 375,
    tokensToday: 890000,
    tokensTotal: 25000000,
    bindings: [
      {
        priority: 1,
        providerGroupId: 'group-openai-vip',
        providerGroupName: 'openai-vip',
        creationTime: '2025-06-15T14:30:00Z',
        supportedRouteProfiles: [RouteProfile.OpenAiResponses, RouteProfile.OpenAiCodex, RouteProfile.ChatCompletions]
      },
      {
        priority: 2,
        providerGroupId: 'group-compatible-fallback',
        providerGroupName: 'compatible-fallback',
        creationTime: '2025-06-15T14:30:00Z',
        supportedRouteProfiles: []
      },
      {
        priority: 3,
        providerGroupId: 'group-default',
        providerGroupName: 'default',
        creationTime: '2025-06-15T14:30:00Z',
        supportedRouteProfiles: [
          RouteProfile.GeminiBeta,
          RouteProfile.GeminiInternal,
          RouteProfile.OpenAiResponses,
          RouteProfile.OpenAiCodex,
          RouteProfile.ChatCompletions,
          RouteProfile.ClaudeMessages
        ]
      }
    ]
  },
  {
    id: 'sub-003',
    userId: '00000000-0000-0000-0000-000000000001',
    name: '已过期测试 Key',
    description: '测试过期逻辑',
    secret: 'sk_expired-test_key_000',
    isActive: false,
    expiresAt: '2025-12-31T23:59:59Z',
    creationTime: '2025-01-01T10:00:00Z',
    usageToday: 0,
    usageTotal: 120,
    costToday: 0,
    costTotal: 0.18,
    tokensToday: 0,
    tokensTotal: 12000,
    bindings: []
  },
  {
    id: 'sub-004',
    userId: '00000000-0000-0000-0000-000000000002',
    name: '个人实验 Key',
    description: '用于个人工作区实验与日常对话',
    secret: 'sk_personal_lab_222222',
    isActive: true,
    creationTime: '2025-03-12T09:30:00Z',
    usageToday: 420,
    usageTotal: 18600,
    costToday: 1.26,
    costTotal: 42.8,
    tokensToday: 58200,
    tokensTotal: 2360000,
    lastUsedAt: '2026-04-20T16:12:00Z',
    bindings: [
      {
        priority: 1,
        providerGroupId: 'group-default',
        providerGroupName: 'default',
        creationTime: '2025-03-12T09:30:00Z',
        supportedRouteProfiles: [
          RouteProfile.GeminiBeta,
          RouteProfile.GeminiInternal,
          RouteProfile.OpenAiResponses,
          RouteProfile.OpenAiCodex,
          RouteProfile.ChatCompletions,
          RouteProfile.ClaudeMessages
        ]
      }
    ]
  },
  {
    id: 'sub-005',
    userId: '00000000-0000-0000-0000-000000000002',
    name: '移动端灰度 Key',
    description: '验证移动端灰度策略与成本表现',
    secret: 'sk_mobile_gray_555555',
    isActive: true,
    expiresAt: '2026-08-31T23:59:59Z',
    creationTime: '2025-07-08T11:20:00Z',
    usageToday: 1280,
    usageTotal: 64000,
    costToday: 4.92,
    costTotal: 188.35,
    tokensToday: 186000,
    tokensTotal: 8640000,
    lastUsedAt: '2026-04-20T18:20:00Z',
    bindings: [
      {
        priority: 1,
        providerGroupId: 'group-openai-vip',
        providerGroupName: 'openai-vip',
        creationTime: '2025-07-08T11:20:00Z',
        supportedRouteProfiles: [RouteProfile.OpenAiResponses, RouteProfile.ChatCompletions]
      }
    ]
  }
];

export function getSubscriptionsByUserId(userId: string) {
  return SUBSCRIPTIONS.filter(item => item.userId === userId);
}

export function getSubscriptionById(id: string) {
  return SUBSCRIPTIONS.find(item => item.id === id);
}
