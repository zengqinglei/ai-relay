import { findGroupById } from './provider-group';
import { getVisibleGroupsForCurrentUser } from './provider-group';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockException, MockRequest } from '../core/models';
import { getAllSubscriptions, SUBSCRIPTIONS } from '../data/subscriptions';
import { getUserByToken } from '../utils/current-user';

const subscriptions = SUBSCRIPTIONS;

function normalizeBindings(bindings: Array<{ priority: number; providerGroupId: string }>, creationTime: string) {
  return bindings.map((binding, index) => {
    const group = findGroupById(binding.providerGroupId);
    return {
      priority: index + 1,
      providerGroupId: binding.providerGroupId,
      providerGroupName: group?.name ?? binding.providerGroupId,
      creationTime,
      supportedRouteProfiles: group?.supportedRouteProfiles ?? []
    };
  });
}

function getVisibleSubscriptions(req: MockRequest) {
  const currentUser = getUserByToken(req);
  const onlyCurrentUser = String(req.queryParams['onlyCurrentUser']) === 'true';
  const isAdmin = currentUser.roles.includes('Admin');

  if (!isAdmin || onlyCurrentUser) {
    return getAllSubscriptions().filter(item => item.userId === currentUser.id);
  }

  return [...getAllSubscriptions()];
}

function getSubscriptions(req: MockRequest) {
  const { keyword, isActive, offset = 0, limit = 10 } = req.queryParams;

  let items = getVisibleSubscriptions(req);

  if (keyword) {
    const k = String(keyword).trim().toLowerCase();
    items = items.filter(s => s.name.toLowerCase().includes(k) || s.username?.toLowerCase().includes(k));
  }

  if (isActive !== undefined && isActive !== null && isActive !== '') {
    const active = String(isActive) === 'true';
    items = items.filter(s => s.isActive === active);
  }

  const totalCount = items.length;
  const start = +offset;
  const end = start + +limit;
  const paginatedItems = items.slice(start, end);

  return {
    totalCount,
    items: paginatedItems
  } as PagedResultDto<(typeof items)[0]>;
}

function getSubscription(req: MockRequest) {
  const id = req.params['id'];
  const sub = getVisibleSubscriptions(req).find(s => s.id === id);
  if (!sub) throw new MockException(404, 'Subscription not found');
  return sub;
}

function createSubscription(req: MockRequest) {
  const currentUser = getUserByToken(req);
  const body = req.body;
  const visibleGroupIds = new Set(getVisibleGroupsForCurrentUser(req).map(group => group.id));
  const invalidBinding = (body.bindings || []).find((binding: { providerGroupId: string }) => !visibleGroupIds.has(binding.providerGroupId));
  if (invalidBinding) {
    throw new MockException(400, { message: `分组不可访问: ${invalidBinding.providerGroupId}` });
  }
  const secret = body.customSecret || `sk_mock_auto_generated_${Math.random().toString(36).substring(7)}`;
  const creationTime = new Date().toISOString();

  const newSub = {
    ...body,
    userId: currentUser.id,
    username: currentUser.username,
    email: currentUser.email,
    id: crypto.randomUUID(),
    secret,
    isActive: true,
    creationTime,
    usageToday: 0,
    usageTotal: 0,
    costToday: 0,
    costTotal: 0,
    tokensToday: 0,
    tokensTotal: 0,
    bindings: normalizeBindings(body.bindings || [], creationTime)
  };
  subscriptions.unshift(newSub);
  return newSub;
}

function updateSubscription(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body;
  const visibleGroupIds = new Set(getVisibleGroupsForCurrentUser(req).map(group => group.id));
  const invalidBinding = (body.bindings || []).find((binding: { providerGroupId: string }) => !visibleGroupIds.has(binding.providerGroupId));
  if (invalidBinding) {
    throw new MockException(400, { message: `分组不可访问: ${invalidBinding.providerGroupId}` });
  }
  const index = subscriptions.findIndex(s => s.id === id && getVisibleSubscriptions(req).some(item => item.id === id));
  if (index === -1) throw new MockException(404, 'Subscription not found');

  subscriptions[index] = {
    ...subscriptions[index],
    ...body,
    bindings: normalizeBindings(body.bindings || [], subscriptions[index].creationTime)
  };
  return subscriptions[index];
}

function deleteSubscription(req: MockRequest) {
  const id = req.params['id'];
  const index = subscriptions.findIndex(s => s.id === id && getVisibleSubscriptions(req).some(item => item.id === id));
  if (index !== -1) {
    subscriptions.splice(index, 1);
  }
  return { success: true };
}

function enableSubscription(req: MockRequest) {
  const id = req.params['id'];
  const index = subscriptions.findIndex(s => s.id === id && getVisibleSubscriptions(req).some(item => item.id === id));
  if (index !== -1) {
    subscriptions[index].isActive = true;
    if (req.body?.expiresAt) {
      subscriptions[index].expiresAt = req.body.expiresAt;
    }
  }
  return { success: true };
}

function disableSubscription(req: MockRequest) {
  const id = req.params['id'];
  const index = subscriptions.findIndex(s => s.id === id && getVisibleSubscriptions(req).some(item => item.id === id));
  if (index !== -1) {
    subscriptions[index].isActive = false;
  }
  return { success: true };
}

export const SUBSCRIPTION_API = {
  'GET /api/v1/api-keys': (req: MockRequest) => getSubscriptions(req),
  'GET /api/v1/api-keys/:id': (req: MockRequest) => getSubscription(req),
  'POST /api/v1/api-keys': (req: MockRequest) => createSubscription(req),
  'PUT /api/v1/api-keys/:id': (req: MockRequest) => updateSubscription(req),
  'DELETE /api/v1/api-keys/:id': (req: MockRequest) => deleteSubscription(req),
  'PATCH /api/v1/api-keys/:id/enable': (req: MockRequest) => enableSubscription(req),
  'PATCH /api/v1/api-keys/:id/disable': (req: MockRequest) => disableSubscription(req)
};


