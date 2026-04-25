import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockException, MockRequest } from '../core/models';
import { ACCOUNT_TOKENS } from '../data/account-token';
import { PROVIDER_GROUPS } from '../data/provider-group';
import { USERS } from '../data/user';
import { getCurrentUserId, getUserByToken } from '../utils/current-user';

const groups = PROVIDER_GROUPS;
const accounts = ACCOUNT_TOKENS;

function buildGroupView(groupId: string) {
  const group = groups.find(item => item.id === groupId);
  if (!group) {
    return undefined;
  }

  const groupAccounts = accounts.filter(account => account.providerGroupIds.includes(groupId));
  const supportedRouteProfiles = Array.from(new Set(groupAccounts.flatMap(account => account.supportedRouteProfiles)));

  return {
    ...group,
    isPublic: !group.assignedUserIds?.length,
    scopeType: group.assignedUserIds?.length ? 'Private' : 'Public',
    assignedUsernames: (group.assignedUserIds ?? [])
      .map(userId => USERS.find(user => user.id === userId)?.username)
      .filter((username): username is string => !!username),
    supportedRouteProfiles,
    accountCount: groupAccounts.length
  };
}

export function findGroupById(id: string) {
  return buildGroupView(id);
}

export function getVisibleGroupsForCurrentUser(req: MockRequest) {
  const currentUserId = getCurrentUserId(req);
  return groups
    .filter(group => !group.assignedUserIds?.length || group.assignedUserIds.includes(currentUserId))
    .map(group => buildGroupView(group.id)!)
    .filter(Boolean);
}

function getGroups(req: MockRequest) {
  const { keyword, offset = 0, limit = 10, assignedUserId, isPublic, onlyCurrentUserVisible } = req.queryParams;
  const currentUser = getUserByToken(req);
  const shouldRestrictToVisible = !currentUser.roles.includes('Admin') || String(onlyCurrentUserVisible) === 'true';

  let list = (shouldRestrictToVisible ? getVisibleGroupsForCurrentUser(req) : groups.map(group => buildGroupView(group.id)!)).filter(Boolean);

  if (assignedUserId) {
    list = list.filter(group => group.assignedUserIds?.includes(String(assignedUserId)));
  }

  if (isPublic !== undefined && isPublic !== '') {
    const publicOnly = String(isPublic) === 'true';
    list = list.filter(group => group.isPublic === publicOnly);
  }

  if (keyword) {
    const query = String(keyword).trim().toLowerCase();
    list = list.filter(group => group.name.toLowerCase().includes(query) || group.description?.toLowerCase().includes(query));
  }

  const totalCount = list.length;
  const start = +offset;
  const end = start + +limit;
  const items = list.slice(start, end);

  return {
    totalCount,
    items
  } as PagedResultDto<(typeof items)[0]>;
}

function getVisibleGroups(req: MockRequest) {
  return getVisibleGroupsForCurrentUser(req);
}

function getGroup(req: MockRequest) {
  const id = req.params['id'];
  const currentUser = getUserByToken(req);
  const visibleGroups = currentUser.roles.includes('Admin')
    ? groups.map(group => group.id)
    : getVisibleGroupsForCurrentUser(req).map(group => group.id);

  if (!visibleGroups.includes(id)) {
    throw new MockException(404, 'Group not found');
  }

  const group = buildGroupView(id);

  if (!group) {
    throw new MockException(404, 'Group not found');
  }

  return group;
}

function createGroup(req: MockRequest) {
  const body = req.body;
  const assignedUserIds = Array.isArray(body.assignedUserIds) ? body.assignedUserIds.filter(Boolean) : [];
  const newGroup = {
    id: `group-${Date.now()}`,
    name: body.name,
    description: body.description,
    assignedUserIds,
    assignedUsernames: assignedUserIds
      .map((userId: string) => USERS.find(user => user.id === userId)?.username)
      .filter((username: string | undefined): username is string => !!username),
    isDefault: false,
    isPublic: assignedUserIds.length === 0,
    scopeType: assignedUserIds.length > 0 ? 'Private' : 'Public',
    enableStickySession: body.enableStickySession ?? true,
    stickySessionExpirationHours: body.enableStickySession === false ? 1 : (body.stickySessionExpirationHours ?? 1),
    rateMultiplier: body.rateMultiplier ?? 1,
    creationTime: new Date().toISOString(),
    supportedRouteProfiles: [],
    accountCount: 0
  };

  groups.unshift(newGroup);
  return buildGroupView(newGroup.id);
}

function updateGroup(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body;
  const index = groups.findIndex(group => group.id === id);

  if (index === -1) {
    throw new MockException(404, 'Group not found');
  }

  const assignedUserIds = Array.isArray(body.assignedUserIds) ? body.assignedUserIds.filter(Boolean) : [];
  const existing = groups[index];
  groups[index] = {
    ...existing,
    name: existing.isDefault ? existing.name : body.name,
    description: body.description,
    assignedUserIds,
    assignedUsernames: assignedUserIds
      .map((userId: string) => USERS.find(user => user.id === userId)?.username)
      .filter((username: string | undefined): username is string => !!username),
    isPublic: assignedUserIds.length === 0,
    scopeType: assignedUserIds.length > 0 ? 'Private' : 'Public',
    enableStickySession: body.enableStickySession,
    stickySessionExpirationHours: body.enableStickySession ? body.stickySessionExpirationHours : 1,
    rateMultiplier: body.rateMultiplier
  };

  return buildGroupView(id);
}

function deleteGroup(req: MockRequest) {
  const id = req.params['id'];
  const index = groups.findIndex(group => group.id === id);

  if (index === -1) {
    return { success: true };
  }

  if (groups[index].isDefault) {
    throw new MockException(400, '默认分组不可删除');
  }

  groups.splice(index, 1);
  accounts.forEach(account => {
    account.providerGroupIds = account.providerGroupIds.filter(groupId => groupId !== id);
    if (!account.providerGroupIds.length) {
      account.providerGroupIds = ['group-default'];
    }
  });

  return { success: true };
}

export const PROVIDER_GROUP_API = {
  'GET /api/v1/provider-groups': (req: MockRequest) => getGroups(req),
  'GET /api/v1/provider-groups/visible': (req: MockRequest) => getVisibleGroups(req),
  'GET /api/v1/provider-groups/:id': (req: MockRequest) => getGroup(req),
  'POST /api/v1/provider-groups': (req: MockRequest) => createGroup(req),
  'PUT /api/v1/provider-groups/:id': (req: MockRequest) => updateGroup(req),
  'DELETE /api/v1/provider-groups/:id': (req: MockRequest) => deleteGroup(req)
};
