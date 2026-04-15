import { MockException, MockRequest } from '../core/models';
import { USERS, toUserOutput } from '../data/user';

export function getUsers(params: any): { total: number; items: any[] } {
  let users = [...USERS];
  const offset = +params.offset;
  const limit = +params.limit;

  if (params.username) {
    users = users.filter(user => user.username.indexOf(params.username) > -1);
  }

  return { total: users.length, items: users.slice(offset, offset + limit).map(toUserOutput) };
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
  'GET /api/v1/user': (req: MockRequest) => getUsers(req.queryParams),
  'GET /api/v1/user/:id': (req: MockRequest) => getUserById(req.params.id),
  'POST /api/v1/user': (req: MockRequest) => addUser(req.body),
  'PUT /api/v1/user/:id': (req: MockRequest) => updateUser(req.params.id, req.body),
  'POST /api/v1/user/avatar': 'ok',
  'POST /api/v1/register': { msg: 'ok' }
};
