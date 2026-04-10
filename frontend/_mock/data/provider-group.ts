import { GroupSchedulingStrategy, ProviderGroupOutputDto } from '../../src/app/features/platform/models/provider-group.dto';
import { AuthMethod } from '../../src/app/shared/models/auth-method.enum';
import { Provider } from '../../src/app/shared/models/provider.enum';
import { RouteProfile } from '../../src/app/shared/models/route-profile.enum';

export const PROVIDER_GROUPS: ProviderGroupOutputDto[] = [
  {
    id: 'group-001',
    name: 'Gemini 免费池',
    description: '用于非敏感业务的免费 Gemini Pro 账号池',
    schedulingStrategy: GroupSchedulingStrategy.WeightedRandom,
    enableStickySession: false,
    stickySessionExpirationHours: 1,
    rateMultiplier: 1.0,
    creationTime: new Date().toISOString(),
    supportedRouteProfiles: [RouteProfile.GeminiBeta, RouteProfile.GeminiInternal],
    accounts: [
      {
        id: 'rel-001',
        accountTokenId: '1',
        accountTokenName: 'gemini-account-01@example.com',
        provider: Provider.Gemini,
        authMethod: AuthMethod.OAuth,
        supportedRouteProfiles: [RouteProfile.GeminiBeta, RouteProfile.GeminiInternal],
        weight: 10,
        priority: 0,
        isActive: true,
        expiresAt: new Date(Date.now() + 3600 * 1000).toISOString(),
        maxConcurrency: 10,
        currentConcurrency: 2
      },
      {
        id: 'rel-002',
        accountTokenId: '2',
        accountTokenName: 'gemini-account-02@example.com',
        provider: Provider.Gemini,
        authMethod: AuthMethod.OAuth,
        supportedRouteProfiles: [RouteProfile.GeminiBeta, RouteProfile.GeminiInternal],
        weight: 5,
        priority: 0,
        isActive: true,
        expiresAt: null,
        maxConcurrency: 5,
        currentConcurrency: 0
      }
    ]
  },
  {
    id: 'group-002',
    name: 'OpenAI 高优先',
    description: 'VIP 客户专用通道',
    schedulingStrategy: GroupSchedulingStrategy.Priority,
    enableStickySession: true,
    stickySessionExpirationHours: 72,
    rateMultiplier: 1.5,
    creationTime: new Date(Date.now() - 86400000).toISOString(),
    supportedRouteProfiles: [RouteProfile.OpenAiResponses, RouteProfile.ChatCompletions],
    accounts: [
      {
        id: 'rel-003',
        accountTokenId: '5',
        accountTokenName: 'openai-standard',
        provider: Provider.OpenAI,
        authMethod: AuthMethod.OAuth,
        supportedRouteProfiles: [RouteProfile.OpenAiResponses, RouteProfile.OpenAiCodex, RouteProfile.ChatCompletions],
        weight: 1,
        priority: 0,
        isActive: true,
        expiresAt: new Date(Date.now() + 7200 * 1000).toISOString(),
        maxConcurrency: 50,
        currentConcurrency: 45
      },
      {
        id: 'rel-004',
        accountTokenId: '6',
        accountTokenName: 'openai-dev',
        provider: Provider.OpenAI,
        authMethod: AuthMethod.ApiKey,
        supportedRouteProfiles: [RouteProfile.OpenAiResponses, RouteProfile.ChatCompletions],
        weight: 1,
        priority: 1,
        isActive: false,
        expiresAt: new Date(Date.now() - 300 * 1000).toISOString(),
        maxConcurrency: 20,
        currentConcurrency: 0
      }
    ]
  },
  {
    id: 'group-003',
    name: '混合多路由池',
    description: '包含多种路由配置文件，用于测试标签折叠逻辑',
    schedulingStrategy: GroupSchedulingStrategy.AdaptiveBalanced,
    enableStickySession: false,
    stickySessionExpirationHours: 0,
    rateMultiplier: 1.0,
    creationTime: new Date(Date.now() - 3600000).toISOString(),
    supportedRouteProfiles: [
      RouteProfile.GeminiBeta,
      RouteProfile.GeminiInternal,
      RouteProfile.ChatCompletions,
      RouteProfile.OpenAiResponses,
      RouteProfile.OpenAiCodex
    ],
    accounts: [
      {
        id: 'rel-005',
        accountTokenId: '10',
        accountTokenName: 'multi-profile-account',
        provider: Provider.OpenAI,
        authMethod: AuthMethod.OAuth,
        supportedRouteProfiles: [
          RouteProfile.ChatCompletions,
          RouteProfile.OpenAiResponses,
          RouteProfile.OpenAiCodex,
          RouteProfile.GeminiBeta
        ],
        weight: 1,
        priority: 0,
        isActive: true,
        expiresAt: null,
        maxConcurrency: 100,
        currentConcurrency: 85
      },
      {
        id: 'rel-006',
        accountTokenId: '11',
        accountTokenName: 'overloaded-account',
        provider: Provider.Gemini,
        authMethod: AuthMethod.OAuth,
        supportedRouteProfiles: [RouteProfile.GeminiInternal],
        weight: 1,
        priority: 1,
        isActive: true,
        expiresAt: new Date(Date.now() + 86400000 * 30).toISOString(),
        maxConcurrency: 10,
        currentConcurrency: 10
      },
      {
        id: 'rel-007',
        accountTokenId: '12',
        accountTokenName: 'disabled-account',
        provider: Provider.Claude,
        authMethod: AuthMethod.ApiKey,
        supportedRouteProfiles: [RouteProfile.ChatCompletions],
        weight: 1,
        priority: 2,
        isActive: false,
        expiresAt: null,
        maxConcurrency: 0,
        currentConcurrency: 0
      }
    ]
  }
];