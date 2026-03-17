import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { ProviderPlatform } from '../../../shared/models/provider-platform.enum';

/**
 * ApiKey 绑定分组输入 DTO
 */
export interface ApiKeyBindGroupInputDto {
  platform: ProviderPlatform;
  providerGroupId: string;
}

/**
 * ApiKey 绑定分组输出 DTO
 */
export interface ApiKeyBindingOutputDto {
  id?: string;
  platform: ProviderPlatform;
  providerGroupId: string;
  providerGroupName: string;
  creationTime: string;
}

/**
 * 创建 ApiKey 输入 DTO
 */
export interface CreateApiKeyInputDto {
  name: string;
  description?: string;
  expiresAt?: string; // null 表示永不过期
  customSecret?: string; // 自定义密钥值（为空则自动生成，6-48位，包含_或-、数字、字母）
  bindings: ApiKeyBindGroupInputDto[];
}

/**
 * 创建订阅输入 DTO (别名)
 */
export type CreateSubscriptionInputDto = CreateApiKeyInputDto;

/**
 * 更新 ApiKey 输入 DTO
 */
export interface UpdateApiKeyInputDto {
  name?: string; // Update logic might allow name change? If not, optional or remove
  description?: string;
  expiresAt?: string;
  bindings: ApiKeyBindGroupInputDto[];
}

/**
 * 更新订阅输入 DTO (别名)
 */
export type UpdateSubscriptionInputDto = UpdateApiKeyInputDto;

/**
 * ApiKey 输出 DTO
 */
export interface ApiKeyOutputDto {
  id: string;
  name: string;
  description?: string;
  secret: string; // 完整密钥，前端控制显示/隐藏
  isActive: boolean;
  expiresAt?: string;
  lastUsedAt?: string;
  usageToday: number; // Mock data
  usageTotal: number;
  creationTime: string;
  bindings: ApiKeyBindingOutputDto[];
}

/**
 * 订阅输出 DTO (别名，用于兼容现有组件)
 */
export type SubscriptionOutputDto = ApiKeyOutputDto;

/**
 * 订阅统计指标 DTO
 */
export interface SubscriptionMetricsOutputDto {
  totalSubscriptions: number;
  activeSubscriptions: number;
  expiringSoon: number; // 7天内过期
  totalUsageToday: number; // Mock data
  usageGrowthRate: number; // Mock data
  topUsageKeys: Array<{ name: string; usage: number }>; // Mock data
}

/**
 * 查询 API Key 分页输入 DTO
 */
export interface GetSubscriptionsInputDto extends PagedRequestDto {
  /** 搜索关键字（名称） */
  keyword?: string;

  /** 是否启用：true | false | undefined（全部） */
  isActive?: boolean;
}
