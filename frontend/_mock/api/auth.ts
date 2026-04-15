import {
  ChangePasswordInputDto,
  ExternalLoginUrlOutputDto,
  LoginOutputDto,
  UpdateCurrentUserInputDto,
  UserOutputDto
} from '../../src/app/features/account/models/account.dto';
import { MockException, MockRequest } from '../core/models';
import { MockUser, USERS, toUserOutput } from '../data/user';

function buildAccessToken(user: MockUser): string {
  return `fake-jwt-token-${user.id}-${Date.now()}`;
}

function getUserByToken(req: MockRequest): MockUser {
  const authHeader = req.headers.get('Authorization');
  if (!authHeader) {
    throw new MockException(401, { code: 40100, message: '未提供认证令牌' });
  }

  const token = authHeader.replace('Bearer ', '');
  const user = USERS.find(item => token.includes(item.id));

  if (!user) {
    throw new MockException(401, { code: 40100, message: '无效的认证令牌' });
  }

  return user;
}

function ensureUsernameAvailable(username: string, currentUserId: string): void {
  const exists = USERS.some(user => user.username === username && user.id !== currentUserId);
  if (exists) {
    throw new MockException(400, { code: 40011, message: '用户名已存在' });
  }
}

function ensureEmailAvailable(email: string, currentUserId: string): void {
  const exists = USERS.some(user => user.email === email && user.id !== currentUserId);
  if (exists) {
    throw new MockException(400, { code: 40012, message: '邮箱已被使用' });
  }
}

/**
 * 模拟登录（仅返回 Token，不返回用户信息）
 */
function login(usernameOrEmail: string, password: string): LoginOutputDto {
  const user = USERS.find(u => u.username === usernameOrEmail || u.email === usernameOrEmail);

  if (user && user.password === password) {
    return {
      accessToken: buildAccessToken(user),
      refreshToken: `fake-refresh-token-${user.id}-${Date.now()}`,
      expiresIn: 3600
    };
  }

  throw new MockException(401, { code: 40100, message: '用户名或密码不正确' });
}

function getCurrentUser(req: MockRequest): UserOutputDto {
  return toUserOutput(getUserByToken(req));
}

function updateCurrentUser(req: MockRequest): UserOutputDto {
  const user = getUserByToken(req);
  const body = req.body as UpdateCurrentUserInputDto;

  const username = body.username.trim();
  const email = body.email.trim();

  if (!username) {
    throw new MockException(400, { code: 40013, message: '用户名不能为空' });
  }

  if (!email) {
    throw new MockException(400, { code: 40014, message: '邮箱不能为空' });
  }

  ensureUsernameAvailable(username, user.id);
  ensureEmailAvailable(email, user.id);

  user.username = username;
  user.email = email;
  user.nickname = body.nickname?.trim() || undefined;
  user.phoneNumber = body.phoneNumber?.trim() || undefined;
  user.avatar = body.avatar?.trim() || undefined;

  return toUserOutput(user);
}

function changePassword(req: MockRequest): 'ok' {
  const user = getUserByToken(req);
  const body = req.body as ChangePasswordInputDto;

  if (user.password !== body.currentPassword) {
    throw new MockException(400, { code: 40001, message: '当前密码不正确' });
  }

  if (body.newPassword !== body.confirmPassword) {
    throw new MockException(400, { code: 40002, message: '两次输入的新密码不一致' });
  }

  if (body.currentPassword === body.newPassword) {
    throw new MockException(400, { code: 40003, message: '新密码不能与当前密码相同' });
  }

  user.password = body.newPassword;
  return 'ok';
}

function getExternalLoginUrl(provider: string): ExternalLoginUrlOutputDto {
  const redirectUri = `${window.location.origin}/auth/callback`;
  const state = Math.random().toString(36).substring(7);

  let loginUrl = '';
  if (provider === 'github') {
    loginUrl = `https://github.com/login/oauth/authorize?client_id=mock_client_id&redirect_uri=${redirectUri}&state=${state}`;
  } else if (provider === 'google') {
    loginUrl = `https://accounts.google.com/o/oauth2/v2/auth?client_id=mock_client_id&redirect_uri=${redirectUri}&state=${state}&response_type=code&scope=email%20profile`;
  }

  return { loginUrl, state };
}

function handleExternalLoginCallback(provider: string, _code: string): LoginOutputDto {
  const user = USERS[0];
  return {
    accessToken: `fake-jwt-token-${user.id}-${provider}-${Date.now()}`,
    refreshToken: `fake-refresh-token-${provider}-${Date.now()}`,
    expiresIn: 3600
  };
}

export const AUTH_API = {
  'POST /api/v1/auth/login': (req: MockRequest) => login(req.body.usernameOrEmail, req.body.password),
  'GET /api/v1/auth/me': (req: MockRequest) => getCurrentUser(req),
  'PUT /api/v1/auth/me': (req: MockRequest) => updateCurrentUser(req),
  'POST /api/v1/auth/change-password': (req: MockRequest) => changePassword(req),
  'GET /api/v1/external-auth/:provider/login-url': (req: MockRequest) => getExternalLoginUrl(req.params.provider),
  'POST /api/v1/external-auth/:provider/callback': (req: MockRequest) => handleExternalLoginCallback(req.params.provider, req.body.code)
};
