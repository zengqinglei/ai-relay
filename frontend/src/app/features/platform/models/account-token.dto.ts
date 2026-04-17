import { AuthMethod } from '../../../shared/models/auth-method.enum';
import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { Provider } from '../../../shared/models/provider.enum';
import { RouteProfile } from '../../../shared/models/route-profile.enum';

// 使用字符串枚举以匹配后端 JSON 序列化
export enum AccountStatus {
  Normal = 'Normal',
  RateLimited = 'RateLimited',
  PartiallyRateLimited = 'PartiallyRateLimited',
  Error = 'Error'
}

export enum RateLimitScope {
  Account = 'Account',
  Model = 'Model'
}

export interface LimitedModelStateDto {
  modelKey: string;
  displayName?: string;
  lockedUntil?: string;
  statusDescription?: string;
}

export interface AccountTokenOutputDto {
  id: string;
  name: string;
  provider: Provider;
  authMethod: AuthMethod;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  isActive: boolean;

  status: AccountStatus;
  statusDescription?: string;
  rateLimitDurationSeconds?: number;
  lockedUntil?: string;
  rateLimitScope: RateLimitScope;
  limitedModels?: LimitedModelStateDto[];
  limitedModelCount?: number;
  maxConcurrency: number;
  currentConcurrency: number;
  priority: number;
  weight: number;
  providerGroupIds: string[];
  supportedRouteProfiles: RouteProfile[];

  fullToken: string;
  accessToken?: string;
  refreshToken?: string;
  creationTime: string;
  tokenObtainedTime?: string;
  expiresIn?: number | null;

  usageToday: number;
  usageTotal: number;
  costToday: number;
  costTotal: number;
  tokensToday: number;
  tokensTotal: number;
  successRateToday: number;
  successRateTotal: number;
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  allowOfficialClientMimic: boolean;
  isCheckStreamHealth: boolean;
}

export interface CreateAccountTokenInputDto {
  name: string;
  provider: Provider;
  authMethod: AuthMethod;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  credential?: string;
  authCode?: string;
  sessionId?: string;
  maxConcurrency: number;
  priority: number;
  weight: number;
  providerGroupIds: string[];
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  rateLimitScope?: RateLimitScope;
  allowOfficialClientMimic?: boolean;
  isCheckStreamHealth?: boolean;
}

export interface UpdateAccountTokenInputDto {
  name?: string;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  credential?: string;
  maxConcurrency?: number;
  priority?: number;
  weight?: number;
  providerGroupIds?: string[];
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  rateLimitScope?: RateLimitScope;
  allowOfficialClientMimic?: boolean;
  isCheckStreamHealth?: boolean;
}

export interface GetAuthUrlInputDto {
  provider: Provider;
}

export interface GetAuthUrlOutputDto {
  authUrl: string;
  sessionId: string;
}

export interface ExchangeCodeInputDto {
  provider: Provider;
  code: string;
  redirectUri?: string;
  session_id?: string;
}

export interface ExchangeCodeOutputDto {
  refreshToken?: string;
  accessToken: string;
  expiresIn?: number;
  email?: string;
}

export interface GetAccountTokenPagedInputDto extends PagedRequestDto {
  keyword?: string;
  provider?: Provider;
  authMethod?: AuthMethod;
  isActive?: boolean;
  providerGroupIds?: string[];
}

export interface AccountTokenMetricsOutputDto {
  totalAccounts: number;
  activeAccounts: number;
  disabledAccounts: number;
  expiringAccounts: number;

  totalUsageToday: number;
  usageGrowthRate: number;

  averageSuccessRate: number;
  abnormalRequests24h: number;

  rotationWarnings: number;
}
