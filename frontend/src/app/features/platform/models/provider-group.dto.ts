import { PagedRequestDto } from '../../../shared/models/paged-request.dto';
import { Provider } from '../../../shared/models/provider.enum';
import { RouteProfile } from '../../../shared/models/route-profile.enum';

export interface ProviderGroupOutputDto {
  id: string;
  name: string;
  description?: string;
  assignedUserIds?: string[];
  assignedUsernames?: string[];
  isDefault?: boolean;
  isPublic: boolean;
  scopeType: 'Public' | 'Private' | string;
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
  creationTime: string;
  supportedRouteProfiles: RouteProfile[];
  accountCount: number;
}

export interface CreateProviderGroupInputDto {
  name: string;
  description?: string;
  assignedUserIds?: string[];
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
}

export interface UpdateProviderGroupInputDto {
  name: string;
  description?: string;
  assignedUserIds?: string[];
  enableStickySession: boolean;
  stickySessionExpirationHours: number;
  rateMultiplier: number;
}

export interface GetProviderGroupsInputDto extends PagedRequestDto {
  keyword?: string;
  provider?: Provider;
  assignedUserId?: string;
  isPublic?: boolean;
  onlyCurrentUserVisible?: boolean;
}
