import { MockException, MockRequest } from '../core/models';
import { MockUser, USERS } from '../data/user';

function resolveToken(authHeader?: string | null): string {
  if (!authHeader) {
    throw new MockException(401, { code: 40100, message: '未提供认证令牌' });
  }

  return authHeader.replace('Bearer ', '');
}

export function buildAccessToken(user: MockUser): string {
  return `fake-jwt-token-${user.id}-${Date.now()}`;
}

export function getUserByAuthHeader(authHeader?: string | null): MockUser {
  const token = resolveToken(authHeader);
  const user = USERS.find(item => token.includes(item.id));

  if (!user) {
    throw new MockException(401, { code: 40100, message: '无效的认证令牌' });
  }

  return user;
}

export function getUserByToken(req: MockRequest): MockUser {
  return getUserByAuthHeader(req.headers.get('Authorization'));
}

export function getCurrentUserId(req: MockRequest): string {
  return getUserByToken(req).id;
}
