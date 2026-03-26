import { GroupSchedulingStrategy, ProviderGroupOutputDto } from '../../src/app/features/platform/models/provider-group.dto';
import { ProviderPlatform } from '../../src/app/shared/models/provider-platform.enum';

export const PROVIDER_GROUPS: ProviderGroupOutputDto[] = [
  {
    id: 'group-001',
    name: 'Gemini 免费池',
    description: '用于非敏感业务的免费 Gemini Pro 账号池',
    platform: ProviderPlatform.GEMINI_OAUTH,
    schedulingStrategy: GroupSchedulingStrategy.WeightedRandom,
    enableStickySession: false,
    stickySessionExpirationHours: 1,
    rateMultiplier: 1.0,
    creationTime: new Date().toISOString(),
    accounts: [
      {
        id: 'rel-001',
        accountTokenId: 'acc-001',
        accountTokenName: 'Gemini Pro - 01',
        weight: 10,
        priority: 0,
        isActive: true,
        expiresIn: 3600,
        tokenObtainedTime: new Date().toISOString(),
        maxConcurrency: 10,
        currentConcurrency: 2
      },
      {
        id: 'rel-002',
        accountTokenId: 'acc-002',
        accountTokenName: 'Gemini Pro - 02',
        weight: 5,
        priority: 0,
        isActive: true,
        expiresIn: null,
        tokenObtainedTime: null,
        maxConcurrency: 5,
        currentConcurrency: 0
      }
    ]
  },
  {
    id: 'group-002',
    name: 'OpenAI 高优先',
    description: 'VIP 客户专用通道',
    platform: ProviderPlatform.OPENAI_APIKEY,
    schedulingStrategy: GroupSchedulingStrategy.Priority,
    enableStickySession: true,
    stickySessionExpirationHours: 72,
    rateMultiplier: 1.5,
    creationTime: new Date(Date.now() - 86400000).toISOString(),
    accounts: [
      {
        id: 'rel-003',
        accountTokenId: 'acc-003',
        accountTokenName: 'GPT-4 Main',
        weight: 1,
        priority: 0,
        isActive: true,
        expiresIn: 7200,
        tokenObtainedTime: new Date().toISOString(),
        maxConcurrency: 50,
        currentConcurrency: 45
      },
      {
        id: 'rel-004',
        accountTokenId: 'acc-004',
        accountTokenName: 'GPT-3.5 Backup',
        weight: 1,
        priority: 1,
        isActive: false,
        expiresIn: 300,
        tokenObtainedTime: new Date(Date.now() - 3600000).toISOString(),
        maxConcurrency: 20,
        currentConcurrency: 0
      } // Expired example
    ]
  }
];
