import { AccountStatus } from '../../src/app/features/platform/models/account-token.dto';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { AuthMethod } from '../../src/app/shared/models/auth-method.enum';
import { Provider } from '../../src/app/shared/models/provider.enum';
import { MockRequest, MockException } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { ACCOUNT_TOKENS, AVAILABLE_MODELS, MOCK_CHAT_STREAM_CHUNKS } from '../data/account-token';

const accounts = [...ACCOUNT_TOKENS];

// Helper to mask token
function maskToken(token: string | undefined): string {
  if (!token || token.length < 12) return '***';
  return `${token.substring(0, 7)}...${token.substring(token.length - 4)}`;
}

// Helper to simulate detail fields
function enrichAccount(account: any) {
  const isOAuth = account.authMethod === AuthMethod.OAuth;
  if (isOAuth) {
    return {
      ...account,
      accessToken: `ya29.mock_access_token_${account.id.substring(0, 6)}...`
    };
  }
  return account;
}

function getAccounts(req: MockRequest) {
  const { keyword, provider, authMethod, isActive, offset = 0, limit = 10 } = req.queryParams;

  let items = accounts.map(enrichAccount);

  if (keyword) {
    const query = String(keyword).trim().toLowerCase();
    items = items.filter(a => a.name.toLowerCase().includes(query));
  }

  if (provider) {
    const targetProvider = String(provider).trim();
    items = items.filter(a => String(a.provider) === targetProvider);
  }

  if (authMethod) {
    const targetAuthMethod = String(authMethod).trim();
    items = items.filter(a => String(a.authMethod) === targetAuthMethod);
  }

  if (isActive !== undefined && isActive !== null && isActive !== '') {
    const active = String(isActive) === 'true';
    items = items.filter(a => a.isActive === active);
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

function getAccount(req: MockRequest) {
  const id = req.params['id'];
  const account = accounts.find(a => a.id === id);
  if (!account) throw new MockException(404, 'Account not found');
  return enrichAccount(account);
}

// Helper to simulate token exchange (internal use)
function simulateTokenExchange(provider: string) {
  const providerStr = String(provider || 'unknown');
  const accessToken = `mock_access_token_${Date.now()}`;
  const refreshToken = `mock_refresh_token_for_${providerStr}_${Date.now()}`;
  const expiresIn = 3600;
  const email = 'mock-user@example.com';
  let scope = 'https://www.googleapis.com/auth/cloud-platform';
  let extra = {};

  if (providerStr === 'Claude') {
    scope = 'org:create_api_key user:profile user:inference user:sessions:claude_code user:mcp_servers';
    extra = {
      organization: { uuid: 'mock-org-uuid' },
      account: { uuid: 'mock-account-uuid', email_address: email }
    };
  } else if (providerStr === 'OpenAI') {
    scope = 'openid profile email offline_access';
    extra = {
      id_token: 'mock_id_token_jwt',
      token_type: 'Bearer'
    };
  }

  return {
    fullToken: refreshToken, // For mock storage
    accessToken,
    expiresIn,
    scope,
    ...extra
  };
}

function createAccount(req: MockRequest) {
  const body = req.body;

  // Handle OAuth flow (exchange code if present)
  let credential = body.credential;
  let expiresIn = null;

  if (body.authCode) {
    console.log('[Mock] Auto-exchanging code for new account:', body.authCode);
    const tokenInfo = simulateTokenExchange(body.provider);
    credential = tokenInfo.fullToken;
    expiresIn = tokenInfo.expiresIn;
  }

  const newAccount = {
    ...body,
    id: crypto.randomUUID(),
    isActive: true,
    creationTime: new Date().toISOString(),
    fullToken: maskToken(credential || 'mock-token-fallback'),
    usageToday: 0,
    usageTotal: 0,
    costToday: 0,
    costTotal: 0,
    tokensToday: 0,
    tokensTotal: 0,
    successRateToday: 0,
    successRateTotal: 0,
    status: AccountStatus.Normal,
    currentConcurrency: 0,
    expiresIn: expiresIn,
    allowOfficialClientMimic: body.allowOfficialClientMimic ?? false
  };
  accounts.unshift(newAccount);
  return newAccount;
}

function updateAccount(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body;
  const index = accounts.findIndex(a => a.id === id);
  if (index === -1) throw new MockException(404, 'Account not found');

  // Handle OAuth flow (exchange code if present)
  let credential = body.credential;
  let expiresIn = accounts[index].expiresIn; // ✅ Keep existing expiry by default

  if (body.authCode) {
    console.log('[Mock] Auto-exchanging code for account update:', body.authCode);
    const tokenInfo = simulateTokenExchange(accounts[index].provider);
    credential = tokenInfo.fullToken;
    expiresIn = tokenInfo.expiresIn;
  }

  const updateData = { ...body };
  if (credential) {
    updateData.fullToken = maskToken(credential);
    // ✅ 只有当 expiresIn 有值时才更新（APIKEY 保持原值，通常为 null）
    if (expiresIn !== null && expiresIn !== undefined) {
      updateData.expiresIn = expiresIn;
    }
  }

  accounts[index] = { ...accounts[index], ...updateData };
  return accounts[index];
}

function deleteAccount(req: MockRequest) {
  const id = req.params['id'];
  const index = accounts.findIndex(a => a.id === id);
  if (index !== -1) {
    accounts.splice(index, 1);
  }
  return { success: true };
}

function enableAccount(req: MockRequest) {
  const id = req.params['id'];
  const index = accounts.findIndex(a => a.id === id);
  if (index !== -1) accounts[index].isActive = true;
  return { success: true };
}

function disableAccount(req: MockRequest) {
  const id = req.params['id'];
  const index = accounts.findIndex(a => a.id === id);
  if (index !== -1) accounts[index].isActive = false;
  return { success: true };
}

function resetStatus(req: MockRequest) {
  const id = req.params['id'];
  const index = accounts.findIndex(a => a.id === id);
  if (index !== -1) {
    accounts[index].status = AccountStatus.Normal;
    accounts[index].lockedUntil = undefined;
    accounts[index].statusDescription = undefined;
  }
  return { success: true };
}

function getOAuthUrl(req: MockRequest) {
  const { provider } = req.queryParams;
  console.log('[Mock] Generate Auth URL for:', provider);

  const sessionId = crypto.randomUUID();
  let authUrl = '';

  if (String(provider) === 'Antigravity') {
    const clientId = '1071006060591-tmhssin2h21lcre235vtolojh4g403ep.apps.googleusercontent.com';
    const redirectUri = 'http://localhost:8085/callback';
    const scopes = [
      'https://www.googleapis.com/auth/cloud-platform',
      'https://www.googleapis.com/auth/userinfo.email',
      'https://www.googleapis.com/auth/userinfo.profile',
      'https://www.googleapis.com/auth/cclog',
      'https://www.googleapis.com/auth/experimentsandconfigs'
    ].join(' ');

    const params = new URLSearchParams({
      client_id: clientId,
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: scopes,
      access_type: 'offline',
      prompt: 'consent',
      include_granted_scopes: 'true',
      state: `mock_state_${Date.now()}`,
      code_challenge: 'mock_code_challenge_string_for_pkce_very_long_string_123',
      code_challenge_method: 'S256'
    });

    authUrl = `https://accounts.google.com/o/oauth2/v2/auth?${params.toString()}`;
  } else if (String(provider) === 'Claude') {
    const clientId = '9d1c250a-e61b-44d9-88ed-5944d1962f5e';
    const redirectUri = 'https://platform.claude.com/oauth/code/callback';
    const scopes = 'org:create_api_key user:profile user:inference user:sessions:claude_code user:mcp_servers';

    const params = new URLSearchParams({
      code: 'true',
      client_id: clientId,
      response_type: 'code',
      redirect_uri: redirectUri,
      scope: scopes,
      code_challenge: 'mock_code_challenge_string_for_pkce_very_long_string_123',
      code_challenge_method: 'S256',
      state: `mock_state_${Date.now()}`
    });

    authUrl = `https://claude.ai/oauth/authorize?${params.toString()}`;
  } else if (String(provider) === 'OpenAI') {
    const clientId = 'app_EMoamEEZ73f0CkXaXp7hrann';
    const redirectUri = 'http://localhost:1455/auth/callback';
    const scopes = 'openid profile email offline_access';

    const params = new URLSearchParams({
      response_type: 'code',
      client_id: clientId,
      redirect_uri: redirectUri,
      scope: scopes,
      state: `mock_state_${Date.now()}`,
      code_challenge: 'mock_code_challenge_string_for_pkce_very_long_string_123',
      code_challenge_method: 'S256',
      id_token_add_organizations: 'true',
      codex_cli_simplified_flow: 'true'
    });

    authUrl = `https://auth.openai.com/oauth/authorize?${params.toString()}`;
  } else {
    // Default to Gemini OAuth
    const clientId = '681255809395-oo8ft2oprdrnp9e3aqf6av3hmdib135j.apps.googleusercontent.com';
    const redirectUri = 'https://codeassist.google.com/authcode';
    const scopes = [
      'https://www.googleapis.com/auth/cloud-platform',
      'https://www.googleapis.com/auth/userinfo.email',
      'https://www.googleapis.com/auth/userinfo.profile'
    ].join(' ');

    const params = new URLSearchParams({
      client_id: clientId,
      redirect_uri: redirectUri,
      response_type: 'code',
      scope: scopes,
      access_type: 'offline',
      prompt: 'consent',
      include_granted_scopes: 'true',
      state: `mock_state_${Date.now()}`,
      code_challenge: 'mock_code_challenge_string_for_pkce_very_long_string_123',
      code_challenge_method: 'S256'
    });

    authUrl = `https://accounts.google.com/o/oauth2/v2/auth?${params.toString()}`;
  }

  return {
    authUrl: authUrl,
    sessionId: sessionId
  };
}

/**
 * POST /api/v1/account-tokens/:id/model-test
 * 账户令牌模型测试（SSE 流式响应）
 */
SSE_MOCK_REGISTRY.register('POST', /\/api\/v1\/account-tokens\/[^/]+\/model-test$/, (body?: unknown) => {
  console.log('[SSE Mock] Account token model test:', body);
  return MOCK_CHAT_STREAM_CHUNKS;
});

function getAvailableModelsForProvider(req: MockRequest) {
  const provider = req.params['provider'] as Provider;
  const accountId = req.params['accountId'] as string | undefined;

  // 无 accountId → 返回静态列表
  if (!accountId) {
    return AVAILABLE_MODELS[provider] ?? [];
  }

  // 有 accountId → 模拟上游拉取
  const account = accounts.find(a => a.id === accountId);
  if (!account) {
    throw new MockException(404, 'Account not found');
  }

  // 模拟上游拉取：ApiKey 类账户返回扩展列表，OAuth 类返回静态
  if (account.provider === Provider.Claude && account.authMethod === AuthMethod.ApiKey) {
    return [...(AVAILABLE_MODELS[provider] ?? []), { label: 'Claude Opus 4.7 (Upstream)', value: 'claude-opus-4-7-preview' }];
  }

  if (account.provider === Provider.Gemini && account.authMethod === AuthMethod.ApiKey) {
    return [...(AVAILABLE_MODELS[provider] ?? []), { label: 'Gemini 3.2 Flash (Upstream)', value: 'gemini-3.2-flash-preview' }];
  }

  // OAuth 类降级静态
  return AVAILABLE_MODELS[provider] ?? [];
}

export const ACCOUNT_TOKEN_API = {
  'GET /api/v1/account-tokens': (req: MockRequest) => getAccounts(req),
  'GET /api/v1/account-tokens/oauth-url': (req: MockRequest) => getOAuthUrl(req),
  'GET /api/v1/account-tokens/:id': (req: MockRequest) => getAccount(req),
  'GET /api/v1/account-tokens/provider/:provider/models': (req: MockRequest) => getAvailableModelsForProvider(req),
  'POST /api/v1/account-tokens': (req: MockRequest) => createAccount(req),
  'PUT /api/v1/account-tokens/:id': (req: MockRequest) => updateAccount(req),
  'DELETE /api/v1/account-tokens/:id': (req: MockRequest) => deleteAccount(req),
  'PATCH /api/v1/account-tokens/:id/enable': (req: MockRequest) => enableAccount(req),
  'PATCH /api/v1/account-tokens/:id/disable': (req: MockRequest) => disableAccount(req),
  'POST /api/v1/account-tokens/:id/reset-status': (req: MockRequest) => resetStatus(req)
};
