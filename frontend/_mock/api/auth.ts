import {
  ChangePasswordInputDto,
  ExternalLoginUrlOutputDto,
  UpdateCurrentUserInputDto,
  UserOutputDto,
  RegisterInputDto,
  SecurityConfigOutputDto,
  CaptchaOutputDto,
  SendEmailCodeInputDto
} from '../../src/app/features/account/models/account.dto';
import { MockException, MockRequest } from '../core/models';
import { USERS, toUserOutput } from '../data/user';
import { MOCK_SESSION_USER_ID, setMockSessionUserId } from '../utils/current-user';

const CAPTCHA_LETTERS = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz';
const CAPTCHA_DIGITS = '23456789';
const CAPTCHA_CHARACTERS = CAPTCHA_LETTERS + CAPTCHA_DIGITS;
const captchaStore = new Map<string, string>();

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

function sessionLogin(usernameOrEmail: string, password: string): 'ok' {
  const user = USERS.find(u => u.username === usernameOrEmail || u.email === usernameOrEmail);

  if (user && user.password === password) {
    setMockSessionUserId(user.id);
    return 'ok';
  }

  throw new MockException(401, { code: 40100, message: '用户名或密码不正确' });
}

function getCurrentUser(_req: MockRequest): UserOutputDto {
  if (!MOCK_SESSION_USER_ID) {
    throw new MockException(401, { code: 40101, message: '未登录' });
  }
  const user = USERS.find(u => u.id === MOCK_SESSION_USER_ID) ?? USERS[0];
  return toUserOutput(user);
}

function updateCurrentUser(req: MockRequest): UserOutputDto {
  const user = USERS.find(u => u.id === MOCK_SESSION_USER_ID) ?? USERS[0];
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
  const user = USERS.find(u => u.id === MOCK_SESSION_USER_ID) ?? USERS[0];
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

function logout(): 'ok' {
  setMockSessionUserId(null);
  return 'ok';
}

function getSecurityConfig(): SecurityConfigOutputDto {
  return { enableEmailVerification: false }; // 可以在此开启用于测试
}

function getCaptcha(): CaptchaOutputDto {
  const bgColors = ['#f0fdf4', '#f8fafc', '#fffbeb', '#fef2f2', '#f0f9ff'];
  const bg = bgColors[Math.floor(Math.random() * bgColors.length)];
  const lineY = Math.floor(Math.random() * 40);
  const angle = Math.floor(Math.random() * 20) - 10;
  const code = generateCaptchaCode(4);
  const token = Math.random().toString(36).substring(7);

  captchaStore.set(token, code);

  const svgContent = `<svg xmlns="http://www.w3.org/2000/svg" width="130" height="44"><rect width="100%" height="100%" fill="${bg}"/><line x1="0" y1="${lineY}" x2="130" y2="${44 - lineY}" stroke="#94a3b8" stroke-width="2" opacity="0.6"/><text x="50%" y="50%" font-size="24" font-family="monospace" fill="#0f172a" font-weight="bold" font-style="italic" letter-spacing="6" dominant-baseline="central" text-anchor="middle" transform="rotate(${angle}, 65, 22)">${code.split('').join(' ')}</text></svg>`;

  const fakeImage = `data:image/svg+xml;base64,${btoa(svgContent)}`;

  return {
    captchaToken: token,
    captchaImageBase64: fakeImage
  };
}

function generateCaptchaCode(length: number): string {
  const chars = [
    CAPTCHA_LETTERS[Math.floor(Math.random() * CAPTCHA_LETTERS.length)],
    CAPTCHA_DIGITS[Math.floor(Math.random() * CAPTCHA_DIGITS.length)],
    ...Array.from({ length: length - 2 }, () => CAPTCHA_CHARACTERS[Math.floor(Math.random() * CAPTCHA_CHARACTERS.length)])
  ];

  for (let i = chars.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }

  return chars.join('');
}

function validateCaptcha(captchaToken: string | undefined, captchaCode: string | undefined): void {
  const code = captchaToken ? captchaStore.get(captchaToken) : undefined;
  captchaStore.delete(captchaToken ?? '');

  if (!code || !captchaCode || code.toLowerCase() !== captchaCode.trim().toLowerCase()) {
    throw new MockException(400, { code: 40015, message: '图形验证码错误，请重新输入' });
  }
}

function sendEmailCode(req: MockRequest): 'ok' {
  const body = req.body as SendEmailCodeInputDto;
  validateCaptcha(body.captchaToken, body.captchaCode);
  return 'ok';
}

function register(req: MockRequest): 'ok' {
  const body = req.body as RegisterInputDto;

  const username = body.username.trim();
  const email = body.email.trim();

  ensureUsernameAvailable(username, '');
  ensureEmailAvailable(email, '');

  validateCaptcha(body.captchaToken, body.captchaCode);

  // 模拟写入用户
  const newUser = {
    id: `user_${Date.now()}`,
    username: username,
    email: email,
    password: body.password,
    roles: ['User'],
    isActive: true,
    creationTime: new Date().toISOString()
  };
  USERS.push(newUser as any);

  // 注册完可按需设置登录态，这里选择不自动登录
  return 'ok';
}

export const AUTH_API = {
  'POST /api/v1/auth/register': (req: MockRequest) => register(req),
  'GET /api/v1/auth/security-config': () => getSecurityConfig(),
  'GET /api/v1/auth/captcha': () => getCaptcha(),
  'POST /api/v1/auth/send-email-code': (req: MockRequest) => sendEmailCode(req),
  'POST /api/v1/auth/logout': () => logout(),
  'POST /api/v1/auth/session-login': (req: MockRequest) => sessionLogin(req.body.usernameOrEmail, req.body.password),
  'GET /api/v1/auth/me': (req: MockRequest) => getCurrentUser(req),
  'PUT /api/v1/auth/me': (req: MockRequest) => updateCurrentUser(req),
  'POST /api/v1/auth/change-password': (req: MockRequest) => changePassword(req),
  'GET /api/v1/external-auth/:provider/login-url': (req: MockRequest) => getExternalLoginUrl(req.params.provider),
  'POST /api/v1/external-auth/:provider/callback': () => {
    setMockSessionUserId(USERS[0].id);
    return {};
  }
};
