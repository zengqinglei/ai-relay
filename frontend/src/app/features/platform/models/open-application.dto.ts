import { PagedRequestDto } from '../../../shared/models/paged-request.dto';

export type OpenApplicationType = 'web' | 'native' | 'service';
export type OpenApplicationClientType = 'public' | 'confidential';
export type OpenApplicationConsentType = 'implicit' | 'explicit' | 'external' | 'systematic';

export interface OpenApplicationOutputDto {
  id: string;
  clientId: string;
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
  settings: Record<string, unknown>;
  properties: Record<string, unknown>;
  hasClientSecret: boolean;
  /** Client Secret 明文 — 仅在创建 Confidential 应用的响应中一次性返回 */
  clientSecret?: string;
  creationTime: string;
}

export interface CreateOpenApplicationInputDto {
  clientId: string;
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
}

export interface UpdateOpenApplicationInputDto {
  displayName?: string;
  applicationType: OpenApplicationType;
  clientType: OpenApplicationClientType;
  consentType: OpenApplicationConsentType;
  redirectUris: string[];
  postLogoutRedirectUris: string[];
  permissions: string[];
  requirements: string[];
}

export interface ResetOpenApplicationSecretOutputDto {
  clientSecret: string;
}

export interface GetOpenApplicationsInputDto extends PagedRequestDto {
  keyword?: string;
  applicationType?: OpenApplicationType;
  clientType?: OpenApplicationClientType;
}
