import { ProviderGroupOutputDto } from '../../src/app/features/platform/models/provider-group.dto';

export const PROVIDER_GROUPS: ProviderGroupOutputDto[] = [
  {
    id: 'group-default',
    name: 'default',
    description: '系统默认分组，所有新建账户默认加入此分组。',
    assignedUserIds: [],
    assignedUsernames: [],
    isDefault: true,
    isPublic: true,
    scopeType: 'Public',
    enableStickySession: true,
    stickySessionExpirationHours: 1,
    rateMultiplier: 1,
    creationTime: '2026-04-01T09:00:00Z',
    supportedRouteProfiles: [],
    accountCount: 0
  },
  {
    id: 'group-openai-vip',
    name: 'openai-vip',
    description: '高优先级 OpenAI 生产分组。',
    assignedUserIds: [],
    assignedUsernames: [],
    isDefault: false,
    isPublic: true,
    scopeType: 'Public',
    enableStickySession: true,
    stickySessionExpirationHours: 24,
    rateMultiplier: 1.2,
    creationTime: '2026-04-02T09:00:00Z',
    supportedRouteProfiles: [],
    accountCount: 0
  },
  {
    id: 'group-gemini-shared',
    name: 'gemini-shared',
    description: 'Gemini 与 Antigravity 共享调用分组。',
    assignedUserIds: ['00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001'],
    assignedUsernames: ['zengql', 'admin'],
    isDefault: false,
    isPublic: false,
    scopeType: 'Private',
    enableStickySession: false,
    stickySessionExpirationHours: 1,
    rateMultiplier: 1,
    creationTime: '2026-04-03T09:00:00Z',
    supportedRouteProfiles: [],
    accountCount: 0
  },
  {
    id: 'group-compatible-fallback',
    name: 'compatible-fallback',
    description: 'OpenAI Compatible 兼容线路，作为兜底分组。',
    assignedUserIds: ['00000000-0000-0000-0000-000000000001'],
    assignedUsernames: ['admin'],
    isDefault: false,
    isPublic: false,
    scopeType: 'Private',
    enableStickySession: false,
    stickySessionExpirationHours: 1,
    rateMultiplier: 0.8,
    creationTime: '2026-04-04T09:00:00Z',
    supportedRouteProfiles: [],
    accountCount: 0
  }
];
