import { MockException, MockRequest } from '../core/models';
import { USERS, toUserOutput } from '../data/user';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';

export function getUsers(params: any): PagedResultDto<any> {
  let users = [...USERS];
  const offset = +(params.offset ?? 0);
  const limit = +(params.limit ?? 10);

  if (params.keyword) {
    const keyword = String(params.keyword).toLowerCase();
    users = users.filter(user => user.username.toLowerCase().includes(keyword) || user.email.toLowerCase().includes(keyword));
  }

  if (params.isActive !== undefined && params.isActive !== '') {
    const isActive = String(params.isActive) === 'true';
    users = users.filter(user => user.isActive === isActive);
  }

  return { totalCount: users.length, items: users.slice(offset, offset + limit).map(toUserOutput) };
}

export function getUserById(id: string) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(400, { code: 40000, message: '用户名已存在' });
  }
  return toUserOutput(user);
}

export function addUser(value: any) {
  const userExists = USERS.some(w => w.username === value.username);
  if (userExists) {
    throw new MockException(400, { code: 40000, message: '用户名已存在' });
  }
  const newUser = {
    ...value,
    id: (USERS.length + 1).toString(),
    createdTime: new Date(),
    lastModifiedTime: new Date(),
    password: value.password || 'Admin@123456'
  };
  USERS.push(newUser);
  return toUserOutput(newUser);
}

export function updateUser(id: string, value: any) {
  const user = USERS.find(w => w.id === id);
  if (!user) {
    throw new MockException(404, { code: 40400, message: '用户不存在或以删除' });
  }
  Object.assign(user, value);
  return toUserOutput(user);
}

export const USER_API = {
  'GET /api/v1/users': (req: MockRequest) => getUsers(req.queryParams),
  'GET /api/v1/users/:id': (req: MockRequest) => getUserById(req.params.id),
  'POST /api/v1/users': (req: MockRequest) => addUser(req.body),
  'PUT /api/v1/users/:id': (req: MockRequest) => updateUser(req.params.id, req.body),
  'POST /api/v1/user/avatar': 'ok',
  'POST /api/v1/register': { msg: 'ok' }
};
