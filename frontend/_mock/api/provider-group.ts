import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockRequest, MockException } from '../core/models';
import { PROVIDER_GROUPS } from '../data/provider-group';

// 简单内存存储
const groups = [...PROVIDER_GROUPS];

export function findGroupById(id: string) {
  return groups.find(g => g.id === id);
}

function getGroups(req: MockRequest) {
  const { keyword, offset = 0, limit = 10 } = req.queryParams;

  let list = [...groups];

  if (keyword) {
    const query = String(keyword).trim().toLowerCase();
    list = list.filter(g => g.name.toLowerCase().includes(query) || g.description?.toLowerCase().includes(query));
  }

  const totalCount = list.length;
  const start = +offset;
  const end = start + +limit;
  const items = list.slice(start, end);

  const result: PagedResultDto<(typeof items)[0]> = {
    totalCount,
    items
  };

  return result;
}

function getGroup(req: MockRequest) {
  const id = req.params['id'];
  const group = groups.find(g => g.id === id);

  if (!group) {
    throw new MockException(404, 'Group not found');
  }

  return group;
}

function createGroup(req: MockRequest) {
  const body = req.body;
  const newGroup = {
    ...body,
    id: `group-${Date.now()}`,
    creationTime: new Date().toISOString(),
    supportedRouteProfiles: body.supportedRouteProfiles || [],
    accounts: (body.accounts || []).map((a: any) => ({
      ...a,
      accountTokenName: a.accountTokenName || `Mock Account ${a.accountTokenId}`
    }))
  };

  groups.unshift(newGroup);
  return newGroup;
}

function updateGroup(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body;
  const index = groups.findIndex(g => g.id === id);

  if (index === -1) {
    throw new MockException(404, 'Group not found');
  }

  const updatedGroup = {
    ...groups[index],
    ...body,
    creationTime: groups[index].creationTime,
    supportedRouteProfiles: body.supportedRouteProfiles || groups[index].supportedRouteProfiles || [],
    accounts: (body.accounts || []).map((a: any) => ({
      ...a,
      accountTokenName: a.accountTokenName || `Mock Account ${a.accountTokenId}`
    }))
  };

  groups[index] = updatedGroup;
  return updatedGroup;
}

function deleteGroup(req: MockRequest) {
  const id = req.params['id'];
  const index = groups.findIndex(g => g.id === id);

  if (index !== -1) {
    groups.splice(index, 1);
  }

  return { success: true };
}

function addAccountToGroup(req: MockRequest) {
  const groupId = req.params['groupId'];
  const body = req.body;
  const groupIndex = groups.findIndex(g => g.id === groupId);

  if (groupIndex === -1) {
    throw new MockException(404, 'Group not found');
  }

  const newAccount = {
    id: `relation-${Date.now()}`,
    accountTokenId: body.accountId,
    accountTokenName: body.accountTokenName || `Mock Account ${body.accountId}`,
    provider: body.provider,
    authMethod: body.authMethod,
    supportedRouteProfiles: body.supportedRouteProfiles || [],
    weight: body.weight || 1,
    priority: body.priority || 0,
    isActive: true
  };

  groups[groupIndex].accounts.push(newAccount);
  return { success: true };
}

function removeAccountFromGroup(req: MockRequest) {
  const groupId = req.params['groupId'];
  const accountId = req.params['accountId'];
  const groupIndex = groups.findIndex(g => g.id === groupId);

  if (groupIndex === -1) {
    throw new MockException(404, 'Group not found');
  }

  const accountIndex = groups[groupIndex].accounts.findIndex((a: any) => a.accountTokenId === accountId);

  if (accountIndex !== -1) {
    groups[groupIndex].accounts.splice(accountIndex, 1);
  }

  return { success: true };
}

export const PROVIDER_GROUP_API = {
  'GET /api/v1/provider-groups': (req: MockRequest) => getGroups(req),
  'GET /api/v1/provider-groups/:id': (req: MockRequest) => getGroup(req),
  'POST /api/v1/provider-groups': (req: MockRequest) => createGroup(req),
  'PUT /api/v1/provider-groups/:id': (req: MockRequest) => updateGroup(req),
  'DELETE /api/v1/provider-groups/:id': (req: MockRequest) => deleteGroup(req),
  'POST /api/v1/provider-groups/:groupId/accounts': (req: MockRequest) => addAccountToGroup(req),
  'DELETE /api/v1/provider-groups/:groupId/accounts/:accountId': (req: MockRequest) => removeAccountFromGroup(req)
};
