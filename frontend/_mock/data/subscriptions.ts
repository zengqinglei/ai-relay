import { SubscriptionOutputDto } from '../../src/app/features/platform/models/subscription.dto';
import { ProviderPlatform } from '../../src/app/shared/models/provider-platform.enum';

export const SUBSCRIPTIONS: SubscriptionOutputDto[] = [
  {
    id: 'sub-001',
    name: '开发环境默认 Key',
    description: '用于本地开发测试',
    secret: 'sk_dev_test_key_123456',
    isActive: true,
    creationTime: '2025-01-01T10:00:00Z',
    usageToday: 150,
    usageTotal: 5000,
    costToday: 0.18,
    costTotal: 6.00,
    tokensToday: 15000,
    tokensTotal: 500000,
    bindings: [
      {
        platform: ProviderPlatform.OPENAI_APIKEY,
        providerGroupId: 'group-002',
        providerGroupName: 'OpenAI 高优先',
        creationTime: '2025-01-01T10:00:00Z'
      }
    ]
  },
  {
    id: 'sub-002',
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
    costTotal: 375.00,
    tokensToday: 890000,
    tokensTotal: 25000000,
    bindings: [
      {
        platform: ProviderPlatform.OPENAI_APIKEY,
        providerGroupId: 'group-002',
        providerGroupName: 'OpenAI 高优先',
        creationTime: '2025-06-15T14:30:00Z'
      },
      {
        platform: ProviderPlatform.GEMINI_OAUTH,
        providerGroupId: 'group-001',
        providerGroupName: 'Gemini 免费池',
        creationTime: '2025-06-15T14:30:00Z'
      }
    ]
  },
  {
    id: 'sub-003',
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
  }
];
