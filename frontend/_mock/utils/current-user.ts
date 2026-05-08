import { MockException, MockRequest } from '../core/models';
import { MockUser, USERS } from '../data/user';

// Mock session state — BFF 模式下使用内存 session 代替 Cookie
export let MOCK_SESSION_USER_ID: string | null = null;

export function setMockSessionUserId(id: string | null) {
  MOCK_SESSION_USER_ID = id;
}

export function getUserByToken(req: MockRequest): MockUser {
  if (!MOCK_SESSION_USER_ID) {
    throw new MockException(401, { code: 40100, message: '未提供认证令牌' });
  }
  const user = USERS.find(item => item.id === MOCK_SESSION_USER_ID);

  if (!user) {
    throw new MockException(401, { code: 40100, message: '无效的认证令牌' });
  }

  return user;
}

export function getCurrentUserId(req: MockRequest): string {
  return getUserByToken(req).id;
}
