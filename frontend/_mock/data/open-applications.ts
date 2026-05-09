import { OpenApplicationOutputDto } from '../../src/app/features/platform/models/open-application.dto';

export interface MockOpenApplication extends OpenApplicationOutputDto {
  clientSecret?: string;
}

export const OPEN_APPLICATIONS: MockOpenApplication[] = [
  {
    id: 'ai-relay-desktop',
    clientId: 'ai-relay-desktop',
    displayName: 'AiRelay Desktop',
    applicationType: 'native',
    clientType: 'public',
    consentType: 'implicit',
    redirectUris: ['ai-relay-desktop://oauth/callback'],
    postLogoutRedirectUris: ['ai-relay-desktop://oauth/logout-callback'],
    permissions: [
      'ept:authorization',
      'ept:end_session',
      'ept:token',
      'gt:authorization_code',
      'gt:refresh_token',
      'rst:code',
      'scp:openid',
      'scp:profile',
      'scp:email',
      'scp:roles',
      'scp:offline_access'
    ],
    requirements: ['ft:pkce'],
    settings: {},
    properties: {},
    hasClientSecret: false,
    creationTime: '2026-05-01T09:00:00Z'
  },
  {
    id: 'ai-relay-web',
    clientId: 'ai-relay-web',
    displayName: 'AiRelay Web',
    applicationType: 'web',
    clientType: 'public',
    consentType: 'implicit',
    redirectUris: ['http://localhost:4200/auth/callback'],
    postLogoutRedirectUris: ['http://localhost:4200/auth/logout-callback'],
    permissions: [
      'ept:authorization',
      'ept:end_session',
      'ept:token',
      'gt:authorization_code',
      'gt:refresh_token',
      'rst:code',
      'scp:openid',
      'scp:profile',
      'scp:email',
      'scp:roles',
      'scp:offline_access'
    ],
    requirements: ['ft:pkce'],
    settings: {},
    properties: {},
    hasClientSecret: false,
    creationTime: '2026-05-02T10:30:00Z'
  },
  {
    id: 'ai-relay-service',
    clientId: 'ai-relay-service',
    displayName: 'AiRelay Service Client',
    applicationType: 'service',
    clientType: 'confidential',
    consentType: 'systematic',
    redirectUris: [],
    postLogoutRedirectUris: [],
    permissions: ['ept:token', 'gt:client_credentials'],
    requirements: [],
    settings: {},
    properties: {},
    hasClientSecret: true,
    creationTime: '2026-05-03T14:15:00Z',
    clientSecret: 'mock-service-secret'
  }
];

export function getAllOpenApplications() {
  return OPEN_APPLICATIONS;
}

export function getOpenApplicationById(id: string) {
  return OPEN_APPLICATIONS.find(item => item.id === id);
}
