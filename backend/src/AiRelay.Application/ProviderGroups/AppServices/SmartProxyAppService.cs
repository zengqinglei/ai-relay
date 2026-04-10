using AiRelay.Application.ProviderAccounts.Dtos;
using AiRelay.Application.ProviderGroups.Dtos;
using AiRelay.Domain.ApiKeys.Entities;
using AiRelay.Domain.ProviderAccounts.DomainServices;
using AiRelay.Domain.ProviderAccounts.Entities;
using AiRelay.Domain.ProviderAccounts.ValueObjects;
using AiRelay.Domain.ProviderGroups.Entities;
using AiRelay.Domain.ProviderGroups.DomainServices;
using AiRelay.Domain.ProviderGroups.DomainServices.SchedulingStrategy.AccountConcurrencyStrategy;
using Leistd.Ddd.Application.AppService;
using Leistd.Ddd.Domain.Repositories;
using Leistd.Exception.Core;
using Leistd.ObjectMapping.Core;
using Microsoft.Extensions.Logging;
using Leistd.Ddd.Infrastructure.Persistence.Repositories;

namespace AiRelay.Application.ProviderGroups.AppServices;

public class SmartProxyAppService(
    ProviderGroupDomainService providerGroupDomainService,
    AccountTokenDomainService accountTokenDomainService,
    AccountResultHandlerDomainService accountResultHandlerDomainService,
    AccountRateLimitDomainService rateLimitDomainService,
    IRepository<ApiKeyProviderGroupBinding, Guid> apiKeyProviderGroupBindingRepository,
    IRepository<AccountToken, Guid> accountRepository,
    IObjectMapper objectMapper,
    IConcurrencyStrategy concurrencyStrategy,
    IQueryableAsyncExecuter queryableAsyncExecuter,
    ILogger<SmartProxyAppService> logger) : BaseAppService, ISmartProxyAppService
{
    public async Task<SelectAccountResultDto> SelectAccountAsync(
        SelectProxyAccountInputDto input,
        CancellationToken cancellationToken = default)
    {
        // 1. 获取 ApiKey 绑定的分组，按优先级排序
        var bindingGroupQuery = await apiKeyProviderGroupBindingRepository
            .GetQueryIncludingAsync(cancellationToken, p => p.ProviderGroup);
            
        var bindings = await queryableAsyncExecuter.ToListAsync(
            bindingGroupQuery
                .Where(b => b.ApiKeyId == input.ApiKeyId)
                .OrderBy(b => b.Priority));

        if (!bindings.Any())
        {
            throw new ForbiddenException($"ApiKey '{input.ApiKeyName}' 未绑定任何资源池，无法选择账户");
        }

        // 2. 依次按优先级从分组中寻找可用的满足模型请求的账号 (级联穿透)
        AccountToken? accountToken = null;
        ProviderGroup? providerGroup = null;
        bool isStickyBound = false;
        int availableCount = 0;

        foreach (var bindingGroup in bindings)
        {
            var result = await providerGroupDomainService.SelectAccountForApiKeyAsync(
                bindingGroup.ProviderGroup,
                input.ApiKeyId,
                input.ApiKeyName,
                input.SessionHash,
                input.ExcludedAccountIds,
                input.ModelId,
                input.AllowedCombinations);

            if (result != null && result.Value.AccountToken != null)
            {
                accountToken = result.Value.AccountToken;
                providerGroup = result.Value.Group;
                isStickyBound = result.Value.IsStickyBound;
                availableCount = result.Value.AvailableCount;
                break; // 找到可用账号，跳出寻址
            }
        }

        if (accountToken == null || providerGroup == null)
        {
            throw new ServiceUnavailableException($"所有绑定的资源池中均没有可用的适配账户以支撑请求 (所需模型: {input.ModelId})");
        }

        // 3. 刷新 Token (如果需要)
        await accountTokenDomainService.RefreshTokenIfNeededAsync(accountToken, cancellationToken);

        if (string.IsNullOrEmpty(accountToken.AccessToken) && accountToken.AuthMethod != AuthMethod.ApiKey) // 仅非APIKey检查AccessToken
        {
            throw new UnauthorizedException($"{accountToken.Provider} 账户 '{accountToken.Name}' 的凭证为空");
        }

        logger.LogDebug("已选定账户: {AccountTokenName} (ProviderGroupId: {ProviderGroupId}, IsStickyBound: {IsStickyBound})",
            accountToken.Name, providerGroup.Id, isStickyBound);

        // 决定等待策略
        var waitPlan = new WaitPlan
        {
            ShouldWait = isStickyBound,
            Timeout = isStickyBound ? TimeSpan.FromSeconds(30) : TimeSpan.FromSeconds(10), // 非粘性也给10s兜底等待
            MaxConcurrency = accountToken.MaxConcurrency,
            IsStickyBound = isStickyBound
        };

        // 预取并发数据
        var concurrencyCount = await concurrencyStrategy.GetConcurrencyCountAsync(accountToken.Id, cancellationToken);
        var contextItems = new Dictionary<string, object>
        {
            ["ConcurrencyCounts"] = new Dictionary<Guid, int> { [accountToken.Id] = concurrencyCount }
        };

        var accountTokenResult = objectMapper.Map<AccountToken, AvailableAccountTokenOutputDto>(accountToken, contextItems);
        var backoffCount = await rateLimitDomainService.GetBackoffCountAsync(accountToken.Id, cancellationToken);

        return new SelectAccountResultDto
        {
            AccountToken = accountTokenResult,
            ProviderGroupId = providerGroup.Id,
            ProviderGroupName = providerGroup.Name,
            GroupRateMultiplier = providerGroup.RateMultiplier,
            WaitPlan = waitPlan,
            AvailableAccountCount = availableCount,
            BackoffCount = backoffCount
        };
    }

    public async Task HandleSuccessAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        await rateLimitDomainService.ClearBackoffCountAsync(accountId, cancellationToken);

        var account = await accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account == null) return;

        if (account.ResetStatus())
        {
            await accountRepository.UpdateAsync(account, cancellationToken);
        }
    }

    public async Task HandleFailureAsync(
        HandleFailureInputDto input,
        CancellationToken cancellationToken = default)
    {
        var account = await accountRepository.GetByIdAsync(input.AccountId, cancellationToken);
        if (account == null) return;

        // 调用领域服务执行熔断/禁用
        await accountResultHandlerDomainService.HandleFailureAsync(
            account,
            input.StatusCode,
            input.ErrorContent,
            input.ErrorAnalysis.IsCanRetry,
            input.ErrorAnalysis.RetryAfter,
            cancellationToken);

        await accountRepository.UpdateAsync(account, cancellationToken);
    }

    public Task<bool> IsRateLimitedAsync(Guid accountId, CancellationToken cancellationToken = default)
        => rateLimitDomainService.IsRateLimitedAsync(accountId, cancellationToken);
}
