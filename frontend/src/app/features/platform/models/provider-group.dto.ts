import { AuthMethod } from '../../../shared/models/auth-method.enum';
import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { Provider } from '../../../shared/models/provider.enum';
import { RouteProfile } from '../../../shared/models/route-profile.enum';

export enum GroupSchedulingStrategy {
  AdaptiveBalanced = 'AdaptiveBalanced',
  WeightedRandom = 'WeightedRandom',
  Priority = 'Priority',
  QuotaPriority = 'QuotaPriority'
}

/**
 * 调度策略详细说明
 */
export const SCHEDULING_STRATEGY_DESCRIPTIONS: Record<GroupSchedulingStrategy, string> = {
  [GroupSchedulingStrategy.AdaptiveBalanced]: `<strong>自适应均衡策略（推荐）</strong><br/>基于剩余容量加权的最少使用算法，智能平衡负载与配额。<br/><br/><strong>核心优势：</strong><br/>• <strong>性能优先：</strong> 自动避开当前拥堵/响应慢的节点<br/>• <strong>配额均衡：</strong> 长期来看确保各账号配额均匀消耗<br/>• <strong>智能防雪崩：</strong> 当某节点卡顿时自动分流<br/><br/><strong>工作原理：</strong><br/>结合实时并发数与每日累计用量计算得分，动态选择当前状态最佳的账户。`,
  [GroupSchedulingStrategy.WeightedRandom]: `<strong>加权随机策略</strong><br/>根据账户配置的权重值随机选择，权重越高被选中概率越大。<br/><br/><strong>适用场景：</strong><br/>• 灰度发布：新账户设置低权重，逐步提升<br/>• 按质量分配：高质量账户设置更高权重<br/>• 成本优化：低成本账户配置高权重<br/><br/><strong>配置说明：</strong><br/>权重范围：1-1000，总权重越大选中概率越高`,
  [GroupSchedulingStrategy.Priority]: `<strong>优先级降级策略</strong><br/>按预设优先级顺序选择账户，高优先级不可用时自动降级。<br/><br/><strong>适用场景：</strong><br/>• 主备切换：主账户故障时自动切换到备用<br/>• 成本控制：主账户优惠价，备用账户正常价<br/>• 质量保证：主账户高质量，备用账户保底<br/><br/><strong>配置说明：</strong><br/>优先级值越小优先级越高（0 > 1 > 2 ...）`,
  [GroupSchedulingStrategy.QuotaPriority]: `<strong>配额优先策略（智能）</strong><br/>优先选择剩余配额最多的账户，最大化资源利用率。<br/><br/><strong>适用场景：</strong><br/>• 支持配额查询的 OAuth 账户<br/>• 避免配额浪费：优先使用快到期的配额<br/>• 智能负载：自动感知配额变化<br/><br/><strong>工作原理：</strong><br/>后台服务每 5 分钟刷新账户配额，优先选择配额高的账户，配额相同时按优先级排序。<br/><br/><strong>注意：</strong><br/>分组中无配额信息的账户（如 API Key 类型）将自动降级为自适应均衡调度。`
};

/**
 * 调度策略显示标签
 */
export const SCHEDULING_STRATEGY_LABELS: Record<GroupSchedulingStrategy, string> = {
  [GroupSchedulingStrategy.AdaptiveBalanced]: '自适应均衡',
  [GroupSchedulingStrategy.WeightedRandom]: '加权随机',
  [GroupSchedulingStrategy.Priority]: '优先级降级',
  [GroupSchedulingStrategy.QuotaPriority]: '配额优先'
};

export interface AddGroupAccountInputDto {
  accountTokenId: string;
  weight: number;
  priority: number;
}

export interface GroupAccountRelationOutputDto {
  id: string;
  providerGroupId?: string;
  accountTokenId: string;
  accountTokenName: string;
  provider: Provider;
  authMethod: AuthMethod;
  supportedRouteProfiles: RouteProfile[];
  weight: number;
  priority: number;
  isActive?: boolean;
  expiresAt?: string | null;
  maxConcurrency?: number;
  currentConcurrency?: number;
}

export interface ProviderGroupOutputDto {
  id: string;
  name: string;
  description?: string;
  schedulingStrategy: GroupSchedulingStrategy;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
  creationTime: string;
  supportedRouteProfiles: RouteProfile[];
  accounts: GroupAccountRelationOutputDto[];
}

export interface CreateProviderGroupInputDto {
  name: string;
  description?: string;
  schedulingStrategy: GroupSchedulingStrategy;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
  accounts: AddGroupAccountInputDto[];
}

export interface UpdateProviderGroupInputDto {
  name: string;
  description?: string;
  schedulingStrategy: GroupSchedulingStrategy;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
  accounts: AddGroupAccountInputDto[];
}

export interface GetProviderGroupsInputDto extends PagedRequestDto {
  keyword?: string;
  provider?: Provider;
}
