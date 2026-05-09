import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { MockException, MockRequest } from '../core/models';
import { USERS, toUserManagementOutput } from '../data/user';

function getQueryValue(value: unknown) {
  const normalized = Array.isArray(value) ? value[0] : value;
  return normalized === undefined || normalized === null || normalized === '' ? undefined : String(normalized);
}

function sortUsers(users: typeof USERS, sorting?: string) {
  if (!sorting) {
    return users.sort((a, b) => a.username.localeCompare(b.username));
  }

  const [field, direction] = sorting.split(' ');
  const order = direction === 'desc' ? -1 : 1;
  const supportedFields = ['username', 'email', 'displayName', 'isActive', 'isEmailVerified', 'creationTime', 'lastLoginTime'];
  if (!supportedFields.includes(field)) {
    return users.sort((a, b) => a.username.localeCompare(b.username));
  }

  return users.sort((a, b) => {
    const left = field === 'displayName' ? a.nickname : a[field as keyof typeof a];
    const right = field === 'displayName' ? b.nickname : b[field as keyof typeof b];
    return String(left ?? '').localeCompare(String(right ?? '')) * order;
  });
}

export function getUsers(params: any): PagedResultDto<any> {
  let users = [...USERS];
  const offset = +(getQueryValue(params.offset) ?? 0);
  const limit = +(getQueryValue(params.limit) ?? 10);
  const keyword = getQueryValue(params.keyword)?.toLowerCase();
  const isActive = getQueryValue(params.isActive);
  const isEmailVerified = getQueryValue(params.isEmailVerified);
  const role = getQueryValue(params.role);
  const sorting = getQueryValue(params.sorting);

  if (keyword) {
    users = users.filter(
      user =>
        user.username.toLowerCase().includes(keyword) ||
        user.email.toLowerCase().includes(keyword) ||
        user.nickname?.toLowerCase().includes(keyword)
    );
  }

  if (isActive !== undefined) {
    users = users.filter(user => user.isActive === (isActive === 'true'));
  }

  if (isEmailVerified !== undefined) {
    users = users.filter(user => user.isEmailVerified === (isEmailVerified === 'true'));
  }

  if (role) {
    users = users.filter(user => user.roles.includes(role));
  }

  users = sortUsers(users, sorting);

  return { totalCount: users.length, items: users.slice(offset, offset + limit).map(toUserManagementOutput) };
}

export function getUserById(id: string) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在' });
  }
  return toUserManagementOutput(user);
}

export function addUser(value: any) {
  const username = String(value.username ?? '').trim();
  const email = String(value.email ?? '').trim();
  const userExists = USERS.some(w => w.username === username || w.email === email);
  if (userExists) {
    throw new MockException(400, { code: 40000, message: '用户名或邮箱已存在' });
  }

  const newUser = {
    id: crypto.randomUUID(),
    username,
    email,
    nickname: value.displayName,
    avatar: value.avatar || `https://api.dicebear.com/7.x/avataaars/svg?seed=${encodeURIComponent(username)}`,
    phoneNumber: value.phoneNumber,
    isActive: value.isActive ?? true,
    isSuperAdmin: false,
    isEmailVerified: value.isEmailVerified ?? false,
    creationTime: new Date().toISOString(),
    roles: value.roles ?? ['Member'],
    password: value.password || 'Admin@123456'
  };
  USERS.push(newUser);
  return toUserManagementOutput(newUser);
}

export function updateUser(id: string, value: any) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在或已删除' });
  }

  Object.assign(user, {
    email: value.email,
    nickname: value.displayName,
    avatar: value.avatar,
    isActive: value.isActive,
    isEmailVerified: value.isEmailVerified,
    roles: value.roles
  });
  return toUserManagementOutput(user);
}

export function enableUser(id: string) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在或已删除' });
  }
  user.isActive = true;
}

export function disableUser(id: string) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在或已删除' });
  }
  user.isActive = false;
}

export function resetPassword(id: string, value: any) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在或已删除' });
  }
  user.password = value.password;
}

export function deleteUser(id: string) {
  const index = USERS.findIndex(w => w.id === id);
  if (index < 0) {
    throw new MockException(404, { code: 40400, message: '用户不存在或已删除' });
  }
  if (USERS[index].isSuperAdmin) {
    throw new MockException(400, { code: 40000, message: '系统内置超级管理员不允许删除' });
  }
  USERS.splice(index, 1);
}

export const USER_API = {
  'GET /api/v1/users': (req: MockRequest) => getUsers(req.queryParams),
  'GET /api/v1/users/:id': (req: MockRequest) => getUserById(req.params.id),
  'POST /api/v1/users': (req: MockRequest) => addUser(req.body),
  'PUT /api/v1/users/:id': (req: MockRequest) => updateUser(req.params.id, req.body),
  'PATCH /api/v1/users/:id/enable': (req: MockRequest) => enableUser(req.params.id),
  'PATCH /api/v1/users/:id/disable': (req: MockRequest) => disableUser(req.params.id),
  'POST /api/v1/users/:id/reset-password': (req: MockRequest) => resetPassword(req.params.id, req.body),
  'DELETE /api/v1/users/:id': (req: MockRequest) => deleteUser(req.params.id),
  'POST /api/v1/user/avatar': 'ok',
  'POST /api/v1/register': { msg: 'ok' }
};
