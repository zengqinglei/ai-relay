import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { ProviderPlatform } from '../../../shared/models/provider-platform.enum';

// ✅ 使用字符串枚举以匹配后端 JSON 序列化
export enum AccountStatus {
  Normal = 'Normal',
  RateLimited = 'RateLimited',
  Error = 'Error'
}

export interface AccountTokenOutputDto {
  id: string;
  name: string;
  platform: ProviderPlatform;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  isActive: boolean;

  status: AccountStatus;
  statusDescription?: string;
  rateLimitDurationSeconds?: number;
  lockedUntil?: string; // Date string
  maxConcurrency: number;
  currentConcurrency: number;

  fullToken: string; // 敏感信息，通常只在编辑时可能需要（视后端安全策略而定），或者作为 API Key 输入
  accessToken?: string; // OAuth Access Token (用于详情展示)
  refreshToken?: string; // OAuth Refresh Token (用于详情展示)
  creationTime: string;
  tokenObtainedTime?: string;
  expiresIn?: number | null; // 秒

  // 统计数据 (通常列表接口会返回简要统计)
  usageToday: number;
  usageTotal: number;
  successRate: number; // 0-100
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  allowOfficialClientMimic: boolean;
}

export interface CreateAccountTokenInputDto {
  name: string;
  platform: ProviderPlatform;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  credential?: string; // token 或 apikey (OAuth 模式下可选，由后端生成)
  authCode?: string; // OAuth 授权码
  sessionId?: string; // OAuth 会话 ID
  maxConcurrency: number;
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  allowOfficialClientMimic?: boolean;
}

export interface UpdateAccountTokenInputDto {
  name?: string;
  extraProperties?: Record<string, string>;
  baseUrl?: string;
  description?: string;
  credential?: string; // 可选更新
  maxConcurrency?: number;
  modelWhites?: string[];
  modelMapping?: Record<string, string>;
  allowOfficialClientMimic?: boolean;
}

export interface GetAuthUrlInputDto {
  platform: ProviderPlatform;
}

export interface GetAuthUrlOutputDto {
  authUrl: string;
  sessionId: string;
}

export interface ExchangeCodeInputDto {
  platform: ProviderPlatform;
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
  platform?: ProviderPlatform;
  isActive?: boolean;
}

export interface AccountTokenMetricsOutputDto {
  totalAccounts: number;
  activeAccounts: number;
  disabledAccounts: number;
  expiringAccounts: number; // 即将过期

  totalUsageToday: number;
  usageGrowthRate: number; // 较昨日增长率

  averageSuccessRate: number;
  abnormalRequests24h: number;

  rotationWarnings: number; // 轮换预警数量
}
