using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.Extensions;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Dto;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Handler;
using AiRelay.Domain.Shared.ExternalServices.ChatModel.Provider;
using AiRelay.Domain.Shared.OAuth.Authorize;
using AiRelay.Domain.UsageRecords.DomainServices;
using AiRelay.Infrastructure.Shared.ExternalServices.ChatModel.ResponseParsing.StreamProcessor;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Application.Contracts.Dtos;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace AiRelay.Application.ProviderAccounts.AppServices;

/// <summary>
/// 账户令牌应用服务
/// </summary>
public class AccountTokenAppService(
    IRepository<AccountToken, Guid> accountTokenRepository,
    IRepository<ProviderGroupAccountRelation, Guid> relationRepository,
    AccountUsageStatisticsDomainService statisticsDomainService,
    AccountTokenDomainService accountTokenDomainService,
    IChatModelHandlerFactory chatModelHandlerFactory,
    AccountRateLimitDomainService accountRateLimitDomainService,
    IModelProvider modelProvider,
    SseResponseStreamProcessor streamProcessor,
    ILogger<AccountTokenAppService> logger,
    IObjectMapper objectMapper,
    IQueryableAsyncExecuter asyncExecuter,
    IServiceProvider serviceProvider,
    IOAuthSessionManager oauthSessionManager,
    IConcurrencyStrategy concurrencyStrategy) : BaseAppService(), IAccountTokenAppService
{
    public async Task<OAuthUrlOutputDto> GetOAuthUrlAsync(GetAuthUrlInputDto input, CancellationToken cancellationToken = default)
    {
        // 1. 获取 OAuth Provider
        var provider = serviceProvider.GetKeyedService<IOAuthProvider>(input.Platform);
        if (provider == null)
            throw new NotFoundException($"平台 {input.Platform} 不支持 OAuth");

        // 2. 生成 PKCE 参数
        var session = new OAuthSession
        {
            State = Guid.NewGuid().ToString("N"),
            CodeVerifier = GenerateCodeVerifier()
        };

        // 3. 存储会话 (关联 Verifier)
        var sessionId = await oauthSessionManager.CreateSessionAsync(session, cancellationToken);

        // 4. 生成 Code Challenge
        var codeChallenge = GenerateCodeChallenge(session.CodeVerifier);

        // 5. 生成授权 URL
        var authUrl = provider.GetAuthorizationUrl(input.Platform, session.State, codeChallenge);

        return new OAuthUrlOutputDto
        {
            AuthUrl = authUrl,
            SessionId = sessionId
        };
    }

    private static string GenerateCodeVerifier()
    {
        // 简单实现: 32字节随机数的 Base64URL
        var bytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Base64UrlEncode(bytes);
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var challengeBytes = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(verifier));
        return Base64UrlEncode(challengeBytes);
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public async IAsyncEnumerable<ChatStreamEvent> DebugModelAsync(
        Guid id,
        ChatMessageInputDto input,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var accountToken = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");
        yield return new ChatStreamEvent(
            SystemMessage: $"开始测试 {accountToken.Platform} 平台账户 {accountToken.Name} ...");

        await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);

        var handler = chatModelHandlerFactory.CreateHandler(
            accountToken.Platform,
            accountToken.AccessToken!,
            accountToken.BaseUrl,
            accountToken.ExtraProperties,
            shouldMimicOfficialClient: true);

        var downContext = handler.CreateDebugDownContext(input.ModelId, input.Message);
        var upContext = await handler.ProcessRequestContextAsync(downContext, 0, cancellationToken);

        var mappedModel = upContext.MappedModelId == downContext.ModelId
            ? input.ModelId
            : $"{input.ModelId} --> {upContext.MappedModelId}";
        yield return new ChatStreamEvent(SystemMessage: $"测试模型 {mappedModel}");

        using var response = await handler.ProxyRequestAsync(upContext, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return new ChatStreamEvent(Error: $"API 错误 ({response.StatusCode}): {errorBody}");
        }

        await foreach (var evt in streamProcessor.ParseSseStreamAsync(response, handler, cancellationToken))
        {
            yield return evt;
        }
    }

    public async Task<IReadOnlyList<ModelOptionOutputDto>> GetAvailableModelsAsync(
        ProviderPlatform platform,
        Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 加载静态基准模型
        var baselineModels = modelProvider.GetAvailableModels(platform);

        AccountToken? accountToken = null;
        if (accountId.HasValue)
        {
            accountToken = await accountTokenRepository.GetByIdAsync(accountId.Value, cancellationToken)
                ?? throw new NotFoundException($"账户不存在: {accountId}");
        }

        IReadOnlyList<ModelOption>? upstreamModels = null;
        if (accountToken != null)
        {
            try
            {
                await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);
                // 2. 创建 Handler
                var handler = chatModelHandlerFactory.CreateHandler(
                    accountToken.Platform,
                    accountToken.AccessToken!,
                    accountToken.BaseUrl,
                    accountToken.ExtraProperties,
                    shouldMimicOfficialClient: true);

                // 3. 拉取上游（10秒超时）
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(10));

                upstreamModels = await handler.GetModelsAsync(cts.Token);

                if (upstreamModels != null && upstreamModels.Count > 0)
                {
                    logger.LogInformation(
                        "上游模型拉取成功: AccountId={AccountId}, Platform={Platform}, Count={Count}",
                        accountId, platform, upstreamModels.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "上游模型拉取失败，降级静态: AccountId={AccountId}, Platform={Platform}",
                    accountId, platform);
            }
        }

        // 4. 以基准模型过滤上游模型（保持基准顺序）
        IReadOnlyList<ModelOption> finalModels;
        if (upstreamModels != null && upstreamModels.Count > 0)
        {
            var upstreamIds = upstreamModels.Select(m => m.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            finalModels = baselineModels.Where(m => upstreamIds.Contains(m.Value)).ToList();

            var filteredOut = upstreamModels.Where(m => !upstreamIds.Contains(m.Value)).Select(m => m.Value).ToList();
            logger.LogInformation(
                "上游模型过滤完成: AccountId={AccountId}, Platform={Platform}, Upstream={UpstreamCount}, Filtered={FilteredCount}, FilteredOut=[{FilteredOut}]",
                accountId, platform, upstreamModels.Count, finalModels.Count, string.Join(", ", filteredOut));
        }
        else
        {
            // 5. 降级使用静态基准模型
            finalModels = baselineModels;
        }

        return objectMapper.Map<IReadOnlyList<ModelOption>, IReadOnlyList<ModelOptionOutputDto>>(finalModels);
    }

    public async Task<PagedResultDto<AccountTokenOutputDto>> GetPagedListAsync(
        GetAccountTokenPagedInputDto input,
        CancellationToken cancellationToken = default)
    {

        var accountQuery = await accountTokenRepository.GetQueryableAsync(cancellationToken);

        // 关键词搜索
        if (!string.IsNullOrWhiteSpace(input.Keyword))
            accountQuery = accountQuery.Where(a => a.Name.Contains(input.Keyword));

        // 平台筛选
        if (input.Platform.HasValue)
            accountQuery = accountQuery.Where(a => a.Platform == input.Platform.Value);

        // 状态筛选
        if (input.IsActive.HasValue)
            accountQuery = accountQuery.Where(a => a.IsActive == input.IsActive.Value);

        // 默认按创建时间倒序
        accountQuery = accountQuery.OrderByDescending(a => a.CreationTime);

        var totalCount = await asyncExecuter.CountAsync(accountQuery, cancellationToken);
        var accounts = await asyncExecuter.ToListAsync(accountQuery
            .Skip(input.Offset)
            .Take(input.Limit), cancellationToken);

        // ✅ 直接在获取数据后映射，传递上下文
        List<AccountTokenOutputDto> results;

        if (accounts.Any())
        {
            var accountIds = accounts.Select(x => x.Id).ToList();

            // 批量获取并发数据和统计数据
            var statsTask = statisticsDomainService.GetListStatisticsAsync(accountIds, cancellationToken);
            var concurrencyTask = concurrencyStrategy.GetConcurrencyCountsAsync(accountIds, cancellationToken);

            await Task.WhenAll(statsTask, concurrencyTask);

            // 传递上下文数据进行映射
            var contextItems = new Dictionary<string, object>
            {
                ["ConcurrencyCounts"] = concurrencyTask.Result,
                ["AccountStats"] = statsTask.Result
            };
            results = objectMapper.Map<List<AccountToken>, List<AccountTokenOutputDto>>(accounts, contextItems);
        }
        else
        {
            results = new List<AccountTokenOutputDto>();
        }

        return new PagedResultDto<AccountTokenOutputDto>(totalCount, results);
    }

    public async Task<AccountTokenOutputDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await accountTokenRepository.GetByIdAsync(id, cancellationToken);
        if (account == null)
            throw new NotFoundException($"账户不存在: {id}");

        // 预取并发数据 (避免 Resolver 中的 Sync-over-Async)
        var concurrencyCount = await concurrencyStrategy.GetConcurrencyCountAsync(id, cancellationToken);
        var contextItems = new Dictionary<string, object>
        {
            ["ConcurrencyCounts"] = new Dictionary<Guid, int> { [id] = concurrencyCount }
        };

        // 1. 基础映射 (传递并发数据)
        var result = objectMapper.Map<AccountToken, AccountTokenOutputDto>(account, contextItems);

        // 2. 填充统计数据
        var stats = await statisticsDomainService.GetListStatisticsAsync([id], cancellationToken);
        if (stats.TryGetValue(id, out var stat))
        {
            result.UsageToday = stat.UsageToday;
            result.UsageTotal = stat.UsageTotal;
            result.SuccessRate = stat.SuccessRate;
        }

        return result;
    }

    public async Task<AccountTokenOutputDto> CreateAsync(
        CreateAccountTokenInputDto input,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始创建账户: {Name} - {Platform}", input.Name, input.Platform);

        // 验证输入参数
        if (input.Platform.IsOAuthPlatform())
        {
            if (string.IsNullOrWhiteSpace(input.AuthCode) || string.IsNullOrWhiteSpace(input.SessionId))
            {
                throw new BadRequestException("OAuth 模式下 AuthCode 和 SessionId 不能为空");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.Credential))
            {
                throw new BadRequestException("API Key 模式下 Credential 不能为空");
            }
        }

        // 检查同名账户
        var existingAccounts = await accountTokenRepository.GetListAsync(
            a => a.Name == input.Name && a.Platform == input.Platform,
            cancellationToken);

        if (existingAccounts.Any())
            throw new BadRequestException($"平台 {input.Platform} 下已存在同名账户: {input.Name}");

        // 判断凭证类型
        string? accessToken = null;
        string? refreshToken = null;
        long? expiresIn = null;
        Dictionary<string, string>? mergedExtraProperties = input.ExtraProperties != null
            ? new Dictionary<string, string>(input.ExtraProperties)
            : new Dictionary<string, string>();

        // OAuth 流程处理
        if (input.Platform.IsOAuthPlatform())
        {
            // 1. 获取 Session
            var session = await oauthSessionManager.GetAndRemoveSessionAsync(input.SessionId!, cancellationToken);
            if (session == null)
                throw new BadRequestException("OAuth 会话无效或已过期");

            // 2. 获取对应的 OAuth Provider
            var provider = serviceProvider.GetKeyedService<IOAuthProvider>(input.Platform);
            if (provider == null)
                throw new BadRequestException($"平台 {input.Platform} 不支持 OAuth 自动交换");

            // 3. 交换 Token (使用 IOAuthProvider 统一接口)
            var tokenResponse = await provider.ExchangeCodeForTokenAsync(
                input.AuthCode!,
                codeVerifier: session.CodeVerifier,
                platform: input.Platform,
                cancellationToken: cancellationToken);

            refreshToken = tokenResponse.RefreshToken;
            accessToken = tokenResponse.AccessToken;
            // 注意: ExpiresIn 是 int? 秒数，accountToken 需要的是 long?
            expiresIn = tokenResponse.ExpiresIn;

            // 合并 OAuth Provider 返回的 ExtraProperties（如 chatgpt_account_id）
            if (tokenResponse.ExtraProperties != null)
            {
                foreach (var kvp in tokenResponse.ExtraProperties)
                {
                    mergedExtraProperties[kvp.Key] = kvp.Value;
                }
            }
        }
        else if (input.Platform.IsApiKeyPlatform())
        {
            // API Key 平台，credential 作为 AccessToken
            accessToken = input.Credential;
            expiresIn = null; // API Key 通常不过期或由提供商控制
        }
        else
        {
            // Account 平台，credential 作为 RefreshToken
            refreshToken = input.Credential;
        }

        // 创建账户，领域服务预热 (刷新Token + 获取ProjectId)
        var accountToken = await accountTokenDomainService.CreateAndPrepareAsync(
            input.Platform,
            input.Name,
            mergedExtraProperties,
            accessToken,
            refreshToken,
            expiresIn,
            input.BaseUrl,
            input.Description,
            input.MaxConcurrency,
            cancellationToken);

        logger.LogInformation("创建账户成功: {Name}", accountToken.Name);

        // ✅ 传递空的上下文以保持统一（新创建的账户暂无并发和统计数据）
        var contextItems = new Dictionary<string, object>
        {
            ["ConcurrencyCounts"] = new Dictionary<Guid, int>(),
            ["AccountStats"] = new Dictionary<Guid, (long UsageToday, long UsageTotal, decimal CostToday, decimal CostTotal)>()
        };

        var result = objectMapper.Map<AccountToken, AccountTokenOutputDto>(accountToken, contextItems);
        return result;
    }

    public async Task<AccountTokenOutputDto> UpdateAsync(
        Guid id,
        UpdateAccountTokenInputDto input,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始更新账户: {Id}", id);

        var accountToken = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");

        // 更新基本信息
        accountToken.Update(input.Name, input.BaseUrl, input.Description, input.MaxConcurrency, input.ExtraProperties);

        // 更新凭证
        if (!string.IsNullOrWhiteSpace(input.Credential))
        {
            if (accountToken.Platform.IsApiKeyPlatform())
            {
                // 对于API Key平台，更新Access Token（expiresIn 传 null 表示保持原值，因为 APIKEY 通常永久有效）
                accountToken.UpdateTokens(input.Credential, null, null);
            }
            else
            {
                // 对于Account平台，更新Refresh Token
                accountToken.UpdateRefreshToken(input.Credential);
            }
        }

        // 也可以在这里调用 PrepareAccountAsync 来验证更新后的凭证，但这可能导致更新操作变慢
        // 且 Update 通常期望是原子的。如果需要，可以考虑异步触发或者让用户手动触发 "Test"
        // 现阶段保持原样，只更新数据。

        await accountTokenRepository.UpdateAsync(accountToken, cancellationToken: cancellationToken);

        logger.LogInformation("更新账户成功: {Id}", id);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("开始删除账户: {Id}", id);

        var account = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");

        // 检查是否被分组关联
        var relations = await relationRepository.GetListAsync(
            r => r.AccountTokenId == id,
            cancellationToken);

        if (relations.Any())
            throw new BadRequestException("账户已被分组关联，无法删除");

        // 软删除
        await accountTokenRepository.DeleteAsync(account, cancellationToken: cancellationToken);

        logger.LogInformation("删除账户成功: {Id}", id);
    }

    public async Task EnableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("启用账户: {Id}", id);

        var account = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");
        account.Enable();
        await accountTokenRepository.UpdateAsync(account, cancellationToken: cancellationToken);

        logger.LogInformation("启用账户成功: {Id}", id);
    }

    public async Task DisableAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("禁用账户: {Id}", id);

        var account = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");
        account.Disable();
        await accountTokenRepository.UpdateAsync(account, cancellationToken: cancellationToken);

        logger.LogInformation("禁用账户成功: {Id}", id);
    }

    public async Task ResetStatusAsync(Guid id, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("重置账户状态: {Id}", id);

        var account = await accountTokenRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"账户不存在: {id}");

        // 使用 RateLimitTracker 清除 Redis 缓存和 DB 状态（支持限流和异常状态）
        await accountRateLimitDomainService.ClearAsync(account, cancellationToken);

        logger.LogInformation("重置账户状态成功: {Id}", id);
    }
}
