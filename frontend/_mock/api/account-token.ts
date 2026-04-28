import { AccountStatus } from '../../src/app/features/platform/models/account-token.dto';
import { ROUTE_PROFILE_SUPPORTED_COMBINATIONS } from '../../src/app/shared/constants/route-profile.constants';
import { AuthMethod } from '../../src/app/shared/models/auth-method.enum';
import { ModelVendor } from '../../src/app/shared/models/model-vendor.enum';
import { PagedResultDto } from '../../src/app/shared/models/paged-result.dto';
import { Provider } from '../../src/app/shared/models/provider.enum';
import { MockException, MockRequest } from '../core/models';
import { SSE_MOCK_REGISTRY } from '../core/sse-mock-registry';
import { ACCOUNT_TOKENS, ANTIGRAVITY_MODELS, AVAILABLE_MODELS, MOCK_CHAT_STREAM_CHUNKS } from '../data/account-token';

const accounts = ACCOUNT_TOKENS;

const DEFAULT_CATALOG_VENDORS: Record<Provider, ModelVendor[]> = {
  [Provider.Gemini]: [ModelVendor.Google],
  [Provider.Claude]: [ModelVendor.Anthropic],
  [Provider.OpenAI]: [ModelVendor.OpenAI],
  [Provider.Antigravity]: [],
  [Provider.OpenAICompatible]: [ModelVendor.Qwen, ModelVendor.Moonshot, ModelVendor.DeepSeek, ModelVendor.MiniMax, ModelVendor.Zhipu, ModelVendor.Jimeng]
};

function maskToken(token: string | undefined): string {
  if (!token || token.length < 12) return '***';
  return `${token.substring(0, 7)}...${token.substring(token.length - 4)}`;
}

function normalizeProviderGroupIds(providerGroupIds?: string[]): string[] {
  return providerGroupIds?.length ? providerGroupIds : ['group-default'];
}

function inferSupportedRouteProfiles(provider: Provider, authMethod: AuthMethod) {
  return Object.entries(ROUTE_PROFILE_SUPPORTED_COMBINATIONS)
    .filter(([, combinations]) => combinations.some(item => item.provider === provider && item.authMethod === authMethod))
    .map(([profile]) => profile);
}

function enrichAccount(account: any) {
  const limitedModels = Array.isArray(account.limitedModels) ? account.limitedModels : [];
  const limitedModelCount = account.limitedModelCount ?? limitedModels.length;
  const status = limitedModelCount > 0 && account.status === AccountStatus.Normal ? AccountStatus.PartiallyRateLimited : account.status;

  const normalized = {
    ...account,
    limitedModels,
    limitedModelCount,
    status
  };

  if (normalized.authMethod === AuthMethod.OAuth) {
    return {
      ...normalized,
      accessToken: `ya29.mock_access_token_${account.id.substring(0, 6)}...`
    };
  }

  return normalized;
}

function getBaselineModels(provider: Provider) {
  if (provider === Provider.Antigravity) {
    return ANTIGRAVITY_MODELS;
  }

  const vendors = DEFAULT_CATALOG_VENDORS[provider] ?? [];
  return vendors.flatMap(vendor => AVAILABLE_MODELS[vendor] ?? []);
}

function isPatternMatch(text: string, pattern: string) {
  const parts = pattern.split('*');
  let pos = 0;

  for (let i = 0; i < parts.length; i++) {
    const part = parts[i];
    if (!part) {
      continue;
    }

    const idx = text.toLowerCase().indexOf(part.toLowerCase(), pos);
    if (idx < 0) {
      return false;
    }

    if (i === 0 && idx !== 0) {
      return false;
    }

    pos = idx + part.length;
  }

  return !parts[parts.length - 1] || pos === text.length;
}

function buildWhitelistModelOptions(whitelist: string[], baselineModels: typeof ANTIGRAVITY_MODELS) {
  const baselineLookup = new Map(baselineModels.map(model => [model.value.toLowerCase(), model]));
  const result: typeof baselineModels = [];
  const seen = new Set<string>();

  for (const entry of whitelist) {
    if (entry.includes('*')) {
      for (const baseline of baselineModels) {
        if (isPatternMatch(baseline.value, entry) && !seen.has(baseline.value.toLowerCase())) {
          seen.add(baseline.value.toLowerCase());
          result.push(baseline);
        }
      }
      continue;
    }

    const baseline = baselineLookup.get(entry.toLowerCase());
    if (baseline) {
      if (!seen.has(baseline.value.toLowerCase())) {
        seen.add(baseline.value.toLowerCase());
        result.push(baseline);
      }
      continue;
    }

    if (!seen.has(entry.toLowerCase())) {
      seen.add(entry.toLowerCase());
      result.push({ label: entry, value: entry });
    }
  }

  return result;
}

function getMockUpstreamModels(account: any) {
  if (account.provider === Provider.Claude && account.authMethod === AuthMethod.ApiKey) {
    return ['claude-opus-4-7-preview', 'claude-opus-4-6', 'claude-sonnet-4-6'];
  }

  if (account.provider === Provider.Gemini && account.authMethod === AuthMethod.ApiKey) {
    return ['gemini-3.1-pro-preview', 'gemini-2.5-pro', 'gemini-2.5-flash'];
  }

  if (account.provider === Provider.OpenAICompatible) {
    return ['Qwen/Qwen3.6-plus', 'Qwen/Qwen3.5-plus', 'deepseek-v4-pro', 'kimi-k2.6'];
  }

  return null;
}

function getAccounts(req: MockRequest) {
  const { keyword, provider, authMethod, isActive, providerGroupIds, offset = 0, limit = 10 } = req.queryParams;

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

  if (providerGroupIds) {
    const targetGroupIds = String(providerGroupIds)
      .split(',')
      .map(id => id.trim())
      .filter(Boolean);

    if (targetGroupIds.length) {
      items = items.filter(account => account.providerGroupIds.some((groupId: string) => targetGroupIds.includes(groupId)));
    }
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
    fullToken: refreshToken,
    accessToken,
    expiresIn,
    scope,
    ...extra
  };
}

function createAccount(req: MockRequest) {
  const body = req.body;

  let credential = body.credential;
  let expiresIn = null;

  if (body.authCode) {
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
    rateLimitScope: body.rateLimitScope ?? 'Account',
    limitedModels: [],
    limitedModelCount: 0,
    currentConcurrency: 0,
    expiresIn,
    priority: body.priority ?? 1,
    weight: body.weight ?? 50,
    providerGroupIds: normalizeProviderGroupIds(body.providerGroupIds),
    supportedRouteProfiles: body.supportedRouteProfiles ?? inferSupportedRouteProfiles(body.provider, body.authMethod),
    allowOfficialClientMimic: body.allowOfficialClientMimic ?? false,
    isCheckStreamHealth: body.isCheckStreamHealth ?? false
  };

  accounts.unshift(newAccount);
  return enrichAccount(newAccount);
}

function updateAccount(req: MockRequest) {
  const id = req.params['id'];
  const body = req.body;
  const index = accounts.findIndex(a => a.id === id);
  if (index === -1) throw new MockException(404, 'Account not found');

  let credential = body.credential;
  let expiresIn = accounts[index].expiresIn;

  if (body.authCode) {
    const tokenInfo = simulateTokenExchange(accounts[index].provider);
    credential = tokenInfo.fullToken;
    expiresIn = tokenInfo.expiresIn;
  }

  const updateData = {
    ...body,
    ...(body.providerGroupIds ? { providerGroupIds: normalizeProviderGroupIds(body.providerGroupIds) } : {})
  };

  if (credential) {
    updateData.fullToken = maskToken(credential);
    if (expiresIn !== null && expiresIn !== undefined) {
      updateData.expiresIn = expiresIn;
    }
  }

  accounts[index] = { ...accounts[index], ...updateData };
  return enrichAccount(accounts[index]);
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
    accounts[index].limitedModels = [];
    accounts[index].limitedModelCount = 0;
  }
  return { success: true };
}

function getOAuthUrl(req: MockRequest) {
  const { provider } = req.queryParams;

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
    authUrl,
    sessionId
  };
}

SSE_MOCK_REGISTRY.register('POST', /\/api\/v1\/account-tokens\/[^/]+\/model-test$/, (body?: unknown) => {
  console.log('[SSE Mock] Account token model test:', body);
  return MOCK_CHAT_STREAM_CHUNKS;
});

function getAvailableModelsForProvider(req: MockRequest) {
  const provider = req.params['provider'] as Provider;
  const accountId = req.queryParams['accountId'] as string | undefined;

  if (!accountId) {
    return getBaselineModels(provider);
  }

  const account = accounts.find(a => a.id === accountId);
  if (!account) {
    throw new MockException(404, 'Account not found');
  }

  const baselineModels = getBaselineModels(account.provider);
  const upstreamModels = getMockUpstreamModels(account);
  if (upstreamModels?.length) {
    const baselineLookup = new Map(baselineModels.map(model => [model.value.toLowerCase(), model]));
    return upstreamModels.map(value => baselineLookup.get(value.toLowerCase()) ?? { label: value, value });
  }

  const whitelist = Array.isArray(account.modelWhites) ? account.modelWhites : [];
  if (whitelist.length > 0) {
    return buildWhitelistModelOptions(whitelist, baselineModels);
  }

  if (account.provider === Provider.OpenAICompatible) {
    throw new MockException(400, { message: '未获取到上游模型列表，且当前账号未配置可用白名单。' });
  }

  return baselineModels;
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
