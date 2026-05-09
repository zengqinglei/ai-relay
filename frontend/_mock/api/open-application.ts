import {
  CreateOpenApplicationInputDto,
  OpenApplicationOutputDto,
  UpdateOpenApplicationInputDto
} from '../../src/app/features/platform/models/open-application.dto';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockException, MockRequest } from '../core/models';
import { MockOpenApplication, OPEN_APPLICATIONS } from '../data/open-applications';

const applications = OPEN_APPLICATIONS;

function toOutput(item: MockOpenApplication): OpenApplicationOutputDto {
  const { clientSecret: _clientSecret, ...output } = item;
  return output;
}

function getQueryValue(value: unknown) {
  const normalized = Array.isArray(value) ? value[0] : value;
  return normalized === undefined || normalized === null || normalized === '' ? undefined : String(normalized);
}

function sortApplications(items: MockOpenApplication[], sorting?: unknown) {
  const expression = getQueryValue(sorting) ?? 'clientId asc';
  const [field, direction = 'asc'] = expression.split(' ');
  const multiplier = direction.toLowerCase() === 'desc' ? -1 : 1;

  return [...items].sort((a, b) => {
    const left = String(a[field as keyof MockOpenApplication] ?? '').toLowerCase();
    const right = String(b[field as keyof MockOpenApplication] ?? '').toLowerCase();
    return left.localeCompare(right) * multiplier;
  });
}

function getOpenApplications(req: MockRequest) {
  const keyword = getQueryValue(req.queryParams['keyword']);
  const applicationType = getQueryValue(req.queryParams['applicationType']);
  const clientType = getQueryValue(req.queryParams['clientType']);
  const offset = getQueryValue(req.queryParams['offset']) ?? '0';
  const limit = getQueryValue(req.queryParams['limit']) ?? '10';
  const sorting = getQueryValue(req.queryParams['sorting']);
  let items = [...applications];

  if (keyword) {
    const value = keyword.trim().toLowerCase();
    items = items.filter(item => item.clientId.toLowerCase().includes(value) || (item.displayName ?? '').toLowerCase().includes(value));
  }

  if (applicationType) {
    items = items.filter(item => item.applicationType === applicationType);
  }

  if (clientType) {
    items = items.filter(item => item.clientType === clientType);
  }

  items = sortApplications(items, sorting);

  const totalCount = items.length;
  const start = +offset;
  const end = start + +limit;

  return {
    totalCount,
    items: items.slice(start, end).map(toOutput)
  } as PagedResultDto<OpenApplicationOutputDto>;
}

function getOpenApplication(req: MockRequest) {
  const id = req.params['id'];
  const application = applications.find(item => item.id === id);
  if (!application) {
    throw new MockException(404, '开放应用不存在');
  }
  return toOutput(application);
}

function validateApplication(input: CreateOpenApplicationInputDto | UpdateOpenApplicationInputDto, id?: string) {
  if ('clientId' in input) {
    const clientId = input.clientId.trim();
    if (!clientId) {
      throw new MockException(400, { message: 'Client ID 不能为空' });
    }
    if (applications.some(item => item.clientId === clientId && item.id !== id)) {
      throw new MockException(400, { message: `Client ID 已存在: ${clientId}` });
    }
  }

  if (input.clientType === 'public' && 'clientSecret' in input && input.clientSecret) {
    throw new MockException(400, { message: 'Public 客户端不能配置 Client Secret' });
  }

  if ((input.applicationType === 'native' || input.clientType === 'public') && !input.requirements.includes('ft:pkce')) {
    throw new MockException(400, { message: 'Native/Public 客户端必须启用 PKCE' });
  }
}

function createOpenApplication(req: MockRequest) {
  const body = req.body as CreateOpenApplicationInputDto;
  validateApplication(body);

  const newApplication: MockOpenApplication = {
    id: body.clientId,
    clientId: body.clientId,
    displayName: body.displayName,
    applicationType: body.applicationType,
    clientType: body.clientType,
    consentType: body.consentType,
    redirectUris: body.redirectUris || [],
    postLogoutRedirectUris: body.postLogoutRedirectUris || [],
    permissions: body.permissions || [],
    requirements: body.requirements || [],
    settings: {},
    properties: {},
    hasClientSecret: body.clientType === 'confidential' && !!body.clientSecret,
    creationTime: new Date().toISOString(),
    clientSecret: body.clientType === 'confidential' ? body.clientSecret : undefined
  };

  applications.unshift(newApplication);
  return toOutput(newApplication);
}

function updateOpenApplication(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body as UpdateOpenApplicationInputDto;
  const index = applications.findIndex(item => item.id === id);
  if (index === -1) {
    throw new MockException(404, '开放应用不存在');
  }

  validateApplication(body, id);

  applications[index] = {
    ...applications[index],
    displayName: body.displayName,
    applicationType: body.applicationType,
    clientType: body.clientType,
    consentType: body.consentType,
    redirectUris: body.redirectUris || [],
    postLogoutRedirectUris: body.postLogoutRedirectUris || [],
    permissions: body.permissions || [],
    requirements: body.requirements || [],
    hasClientSecret: body.clientType === 'confidential' && applications[index].hasClientSecret,
    clientSecret: body.clientType === 'confidential' ? applications[index].clientSecret : undefined
  };

  return toOutput(applications[index]);
}

function deleteOpenApplication(req: MockRequest) {
  const id = req.params['id'];
  const index = applications.findIndex(item => item.id === id);
  if (index !== -1) {
    applications.splice(index, 1);
  }
  return { success: true };
}

function resetOpenApplicationSecret(req: MockRequest) {
  const id = req.params['id'];
  const application = applications.find(item => item.id === id);
  if (!application) {
    throw new MockException(404, '开放应用不存在');
  }
  if (application.clientType !== 'confidential') {
    throw new MockException(400, { message: '只有 Confidential 客户端可以重置密钥' });
  }

  const clientSecret = `mock_secret_${Math.random().toString(36).slice(2, 14)}`;
  application.clientSecret = clientSecret;
  application.hasClientSecret = true;
  return { clientSecret };
}

export const OPEN_APPLICATION_API = {
  'GET /api/v1/open-applications': (req: MockRequest) => getOpenApplications(req),
  'GET /api/v1/open-applications/:id': (req: MockRequest) => getOpenApplication(req),
  'POST /api/v1/open-applications': (req: MockRequest) => createOpenApplication(req),
  'PUT /api/v1/open-applications/:id': (req: MockRequest) => updateOpenApplication(req),
  'DELETE /api/v1/open-applications/:id': (req: MockRequest) => deleteOpenApplication(req),
  'POST /api/v1/open-applications/:id/reset-secret': (req: MockRequest) => resetOpenApplicationSecret(req)
};
