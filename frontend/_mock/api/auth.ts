import { LoginOutputDto, UserOutputDto, ExternalLoginUrlOutputDto } from '../../src/app/features/account/models/account.dto';
import { MockException, MockRequest } from '../core/models';
import { USERS } from '../data/user';

/**
 * 模拟登录（仅返回 Token，不返回用户信息）
 */
function login(usernameOrEmail: string, password: string): LoginOutputDto {
  // 查找用户
  const user = USERS.find(u => u.username === usernameOrEmail || u.email === usernameOrEmail);

  // 验证密码
  if (user && user.username === 'admin' && password === 'Admin@123456') {
    const tokenKey = user.username === 'admin' ? 'fake-jwt-token-admin' : 'fake-jwt-token-testuser';

    return {
      accessToken: `${tokenKey}-${Date.now()}`,
      refreshToken: `fake-refresh-token-${user.id}-${Date.now()}`,
      expiresIn: 3600
    };
  }

  // 登录失败
  throw new MockException(401, { code: 40100, message: '用户名或密码不正确' });
}

/**
 * 获取当前用户信息（根据 Token）
 */
function getCurrentUser(req: MockRequest): UserOutputDto {
  // 从请求头获取 Token
  const authHeader = req.headers.get('Authorization');
  if (!authHeader) {
    throw new MockException(401, { code: 40100, message: '未提供认证令牌' });
  }

  const token = authHeader.replace('Bearer ', '');

  // 根据 Token 查找用户（简化处理）
  if (token.includes('admin')) {
    return USERS[0];
  } else if (token.includes('testuser')) {
    return USERS[1];
  }

  // Token 无效
  throw new MockException(401, { code: 40100, message: '无效的认证令牌' });
}

/**
 * 获取第三方登录 URL
 */
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

/**
 * 第三方登录回调（Mock 环境直接返回 Token）
 */
function handleExternalLoginCallback(provider: string, _code: string): LoginOutputDto {
  // Mock 环境下，直接返回管理员 Token
  return {
    accessToken: `fake-jwt-token-admin-${provider}-${Date.now()}`,
    refreshToken: `fake-refresh-token-${provider}-${Date.now()}`,
    expiresIn: 3600
  };
}

export const AUTH_API = {
  'POST /api/v1/auth/login': (req: MockRequest) => login(req.body.usernameOrEmail, req.body.password),
  'GET /api/v1/auth/me': (req: MockRequest) => getCurrentUser(req),
  'GET /api/v1/external-auth/:provider/login-url': (req: MockRequest) => getExternalLoginUrl(req.params.provider),
  'POST /api/v1/external-auth/:provider/callback': (req: MockRequest) => handleExternalLoginCallback(req.params.provider, req.body.code)
};
